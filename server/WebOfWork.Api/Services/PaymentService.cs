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
    Task<List<PaymentResponse>> GetFreelancerTransactionHistoryAsync(Guid userId, CancellationToken ct);
    Task<List<PaymentResponse>> GetPlatformTransactionsAsync(CancellationToken ct);
}

public class PaymentService : IPaymentService
{
    private readonly WebOfWorkDbContext _db;
    private readonly IPayPalOrderVerifier _payPalVerifier;
    private readonly ICommissionCalculator _commissionCalculator;
    private readonly ISmsSender _smsSender;
    private readonly SmsStrategyFactory _smsFactory;

    public PaymentService(
        WebOfWorkDbContext db,
        IPayPalOrderVerifier payPalVerifier,
        ICommissionCalculator commissionCalculator,
        ISmsSender smsSender)
    {
        _db = db;
        _payPalVerifier = payPalVerifier;
        _commissionCalculator = commissionCalculator;
        _smsSender = smsSender;
        _smsFactory = new SmsStrategyFactory();
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

        Milestone? milestone = null;
        decimal expectedAmountUsd;

        if (request.MilestoneId.HasValue)
        {
            milestone = await _db.Milestones
                .FirstOrDefaultAsync(m => m.Id == request.MilestoneId.Value && m.ContractId == contract.Id, ct);
            if (milestone == null) throw new ArgumentException("Milestone not found for this contract.");
            if (milestone.Status != MilestoneStatus.Pending)
                throw new ArgumentException("Milestone has already been funded.");
            if (await _db.Payments.AnyAsync(p => p.MilestoneId == milestone.Id, ct))
                throw new ArgumentException("Milestone payment already processed.");

            expectedAmountUsd = milestone.Amount;
        }
        else
        {
            expectedAmountUsd = contract.TotalAmount;
        }

        if (request.PayPalAmountUsd != expectedAmountUsd)
            throw new ArgumentException("PayPal amount does not match the contract or milestone amount.");

        var isValid = await _payPalVerifier.VerifyOrderAsync(request.PayPalOrderId, request.PayPalAmountUsd);
        if (!isValid) throw new ArgumentException("Invalid PayPal order. Amount or currency mismatch.");

        using var transaction = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            var fee = await _commissionCalculator.CalculateFeeAsync(expectedAmountUsd);
            
            var payment = new Payment
            {
                ContractId = contract.Id,
                MilestoneId = request.MilestoneId,
                PayPalOrderId = request.PayPalOrderId,
                Amount = expectedAmountUsd,
                PlatformFee = fee,
                Status = PaymentStatus.Escrow,
                ProcessedAt = DateTime.UtcNow
            };
            
            _db.Payments.Add(payment);

            if (milestone != null)
            {
                milestone.Status = MilestoneStatus.InProgress;
                milestone.FundedAt = DateTime.UtcNow;
                milestone.StartedAt = DateTime.UtcNow;
            }

            if (request.MilestoneId.HasValue)
            {
                var otherPendingMilestones = await _db.Milestones
                    .CountAsync(m => m.ContractId == contract.Id
                                  && m.Status == MilestoneStatus.Pending
                                  && m.Id != milestone!.Id, ct);
                contract.IsFunded = otherPendingMilestones == 0;
            }
            else
            {
                contract.IsFunded = true;
            }
            if (contract.Status == ContractStatus.Offered)
            {
                contract.Status = ContractStatus.Active;
            }

            // Reject other proposals if any
            var proposals = await _db.Proposals
                .Where(p => p.ProjectId == contract.ProjectId && p.Status == ProposalStatus.Pending)
                .ToListAsync(ct);
            
            foreach(var prop in proposals)
            {
                prop.Status = prop.FreelancerId == contract.FreelancerId ? ProposalStatus.Accepted : ProposalStatus.Rejected;
            }

            // Update Platform Analytics
            var analytics = await _db.PlatformAnalytics.FirstOrDefaultAsync(ct);
            if (analytics == null)
            {
                analytics = new PlatformAnalytics();
                _db.PlatformAnalytics.Add(analytics);
            }
            analytics.TotalVolumeUsd += expectedAmountUsd;
            analytics.TotalProfitUsd += fee;

            await _db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            // Send SMS
            if (!string.IsNullOrEmpty(contract.Freelancer.Phone) && contract.Freelancer.IsPhoneVerified)
            {
                var msg = _smsFactory.GetStrategy("payment_verified").GenerateMessage(payment.Amount, payment.ContractId);
                await _smsSender.SendSmsAsync(contract.Freelancer.Phone, msg);
            }

            return new PaymentResponse
            {
                Id = payment.Id,
                ContractId = payment.ContractId,
                MilestoneId = payment.MilestoneId,
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

    public async Task<List<PaymentResponse>> GetFreelancerTransactionHistoryAsync(Guid userId, CancellationToken ct)
    {
        var freelancerProfile = await _db.FreelancerProfiles.FirstOrDefaultAsync(f => f.UserId == userId, ct);
        if (freelancerProfile == null) return new List<PaymentResponse>();

        var payments = await _db.Payments
            .Include(p => p.Contract)
            .Where(p => p.Contract.FreelancerId == freelancerProfile.Id && p.Status == PaymentStatus.Released)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync(ct);

        return payments.Select(p => new PaymentResponse
        {
            Id = p.Id,
            ContractId = p.ContractId,
            MilestoneId = p.MilestoneId,
            PayPalOrderId = p.PayPalOrderId,
            Amount = p.Amount - p.PlatformFee, // freelancer receives amount minus fee
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
            MilestoneId = p.MilestoneId,
            PayPalOrderId = p.PayPalOrderId,
            Amount = p.Amount,
            Status = p.Status.ToString(),
            CreatedAt = p.CreatedAt
        }).ToList();
    }
}


