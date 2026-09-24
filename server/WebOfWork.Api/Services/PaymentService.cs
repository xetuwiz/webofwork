using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using WebOfWork.Api.Data;
using WebOfWork.Api.DTOs.Payments;
using WebOfWork.Api.Models;
using WebOfWork.Api.Models.Enums;
using WebOfWork.Api.Integrations.PayPal;
using WebOfWork.Api.Integrations.Sms;
using WebOfWork.Api.Integrations.Sms.Strategies;

namespace WebOfWork.Api.Services;

public interface IPaymentService
{
    Task<PaymentResponse> VerifyPaymentAsync(Guid userId, VerifyPaymentRequest request, CancellationToken ct);
    Task<decimal> GetClientSpendingSummaryAsync(Guid userId, CancellationToken ct);
    Task<List<PaymentResponse>> GetFreelancerTransactionHistoryAsync(Guid userId, string? range, CancellationToken ct);
    Task<List<PaymentResponse>> GetPlatformTransactionsAsync(CancellationToken ct);
}

public class PaymentService : IPaymentService
{
    private readonly WebOfWorkDbContext _db;
    private readonly IPayPalOrderVerifier _payPalVerifier;
    private readonly ICommissionCalculator _commissionCalculator;
    private readonly ISmsSender _smsSender;
    private readonly SmsStrategyFactory _smsFactory;
    private readonly INotificationPublisher? _notificationPublisher;
    private readonly IPayPalClient? _payPalClient;

    public PaymentService(
        WebOfWorkDbContext db,
        IPayPalOrderVerifier payPalVerifier,
        ICommissionCalculator commissionCalculator,
        ISmsSender smsSender,
        INotificationPublisher? notificationPublisher = null,
        IPayPalClient? payPalClient = null)
    {
        _db = db;
        _payPalVerifier = payPalVerifier;
        _commissionCalculator = commissionCalculator;
        _smsSender = smsSender;
        _smsFactory = new SmsStrategyFactory();
        _notificationPublisher = notificationPublisher;
        _payPalClient = payPalClient;
    }

    public async Task<PaymentResponse> VerifyPaymentAsync(Guid userId, VerifyPaymentRequest request, CancellationToken ct)
    {
        var clientProfile = await _db.ClientProfiles.FirstOrDefaultAsync(c => c.UserId == userId, ct);
        if (clientProfile == null) throw new UnauthorizedAccessException("Only clients can verify payments.");

        var contract = await _db.Contracts
            .Include(c => c.Project)
            .Include(c => c.Freelancer)
            .FirstOrDefaultAsync(c => c.Id == request.ContractId, ct);

        if (contract == null) throw new ArgumentException("Contract not found.");
        if (contract.ClientId != clientProfile.Id) throw new UnauthorizedAccessException("You do not own this project/contract.");

        var existingPayment = await _db.Payments.AnyAsync(p => p.PayPalOrderId == request.PayPalOrderId, ct);
        if (existingPayment) throw new ArgumentException("Payment order ID already processed.");

        var expectedAmountUsd = contract.TotalAmount;

        if (request.PayPalAmountUsd != expectedAmountUsd)
            throw new ArgumentException("PayPal amount does not match the contract amount.");

        var isValid = await _payPalVerifier.VerifyOrderAsync(request.PayPalOrderId, request.PayPalAmountUsd);
        if (!isValid) throw new ArgumentException("Invalid PayPal order. Amount or currency mismatch.");

        using var transaction = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            var fee = await _commissionCalculator.CalculateFeeAsync(expectedAmountUsd);
            var orderDetails = _payPalClient == null ? null : await _payPalClient.GetOrderDetailsAsync(request.PayPalOrderId);

            var payment = new Payment
            {
                ContractId = contract.Id,
                PayPalOrderId = request.PayPalOrderId,
                Amount = expectedAmountUsd,
                PlatformFee = fee,
                FreelancerShare = expectedAmountUsd - fee,
                PayPalCaptureId = orderDetails?.CaptureId ?? request.PayPalOrderId,
                Status = PaymentStatus.Escrow,
                ProcessedAt = DateTime.UtcNow
            };

            _db.Payments.Add(payment);

            contract.IsFunded = true;
            if (contract.Status == ContractStatus.Offered)
            {
                contract.Status = ContractStatus.Active;
            }

            // Reject other proposals if any
            var proposals = await _db.Proposals
                .Where(p => p.ProjectId == contract.ProjectId && p.Status == ProposalStatus.Pending)
                .ToListAsync(ct);

            foreach (var prop in proposals)
            {
                prop.Status = prop.FreelancerId == contract.FreelancerId ? ProposalStatus.Accepted : ProposalStatus.Rejected;
            }

            // Atomically increment platform analytics.
            await _db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO platform_analytics (id, total_volume_usd, total_profit_usd, completed_contracts_count, updated_at)
                VALUES (1, {expectedAmountUsd}, {fee}, 0, {DateTime.UtcNow})
                ON CONFLICT (id) DO UPDATE SET
                    total_volume_usd = platform_analytics.total_volume_usd + EXCLUDED.total_volume_usd,
                    total_profit_usd = platform_analytics.total_profit_usd + EXCLUDED.total_profit_usd,
                    updated_at = EXCLUDED.updated_at
                """, ct);

            await _db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            // Send SMS
            if (!string.IsNullOrEmpty(contract.Freelancer.Phone) && contract.Freelancer.IsPhoneVerified)
            {
                var msg = _smsFactory.GetStrategy("payment_verified").GenerateMessage(payment.Amount, payment.ContractId);
                await _smsSender.SendSmsAsync(contract.Freelancer.Phone, msg);
            }

            if (_notificationPublisher != null)
            {
                await _notificationPublisher.PublishAsync(
                    contract.Freelancer.UserId,
                    NotificationType.PaymentCredited,
                    $"Payment for contract #{payment.ContractId} has been credited to escrow.",
                    "Payment",
                    payment.Id,
                    ct,
                    payment.Amount,
                    payment.ContractId);
            }

            return new PaymentResponse
            {
                Id = payment.Id,
                ContractId = payment.ContractId,
                PayPalOrderId = payment.PayPalOrderId,
                Amount = payment.Amount,
                Status = payment.Status.ToString(),
                CreatedAt = payment.CreatedAt
            };
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<decimal> GetClientSpendingSummaryAsync(Guid userId, CancellationToken ct)
    {
        var clientProfile = await _db.ClientProfiles.FirstOrDefaultAsync(c => c.UserId == userId, ct);
        if (clientProfile == null) return 0m;

        return await _db.Payments
            .Include(p => p.Contract)
            .Where(p => p.Contract.ClientId == clientProfile.Id && p.Status != PaymentStatus.Refunded)
            .SumAsync(p => p.Amount, ct);
    }

    public async Task<List<PaymentResponse>> GetFreelancerTransactionHistoryAsync(Guid userId, string? range, CancellationToken ct)
    {
        var freelancerProfile = await _db.FreelancerProfiles.FirstOrDefaultAsync(f => f.UserId == userId, ct);
        if (freelancerProfile == null) return new List<PaymentResponse>();

        var from = range?.ToLowerInvariant() switch
        {
            "1m" => DateTime.UtcNow.AddMonths(-1),
            "6m" => DateTime.UtcNow.AddMonths(-6),
            "1y" => DateTime.UtcNow.AddYears(-1),
            _ => DateTime.MinValue
        };
        var query = _db.Payments.Include(p => p.Contract).Where(p => p.Contract.FreelancerId == freelancerProfile.Id && p.Status == PaymentStatus.Released);
        if (from != DateTime.MinValue) query = query.Where(p => p.CreatedAt >= from);
        var payments = await query.OrderByDescending(p => p.CreatedAt).ToListAsync(ct);

        return payments.Select(p => new PaymentResponse
        {
            Id = p.Id,
            ContractId = p.ContractId,
            PayPalOrderId = p.PayPalOrderId,
            PayPalCaptureId = p.PayPalCaptureId,
            FreelancerShare = p.FreelancerShare == 0m ? p.Amount - p.PlatformFee : p.FreelancerShare,
            Amount = p.Amount - p.PlatformFee,
            Status = p.Status.ToString(),
            CreatedAt = p.CreatedAt
        }).ToList();
    }

    public async Task<List<PaymentResponse>> GetPlatformTransactionsAsync(CancellationToken ct)
    {
        var payments = await _db.Payments
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync(ct);

        return payments.Select(p => new PaymentResponse
        {
            Id = p.Id,
            ContractId = p.ContractId,
            PayPalOrderId = p.PayPalOrderId,
            PayPalCaptureId = p.PayPalCaptureId,
            FreelancerShare = p.FreelancerShare == 0m ? p.Amount - p.PlatformFee : p.FreelancerShare,
            Amount = p.Amount,
            Status = p.Status.ToString(),
            CreatedAt = p.CreatedAt
        }).ToList();
    }
}


