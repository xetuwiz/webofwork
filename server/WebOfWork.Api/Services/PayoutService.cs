using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using WebOfWork.Api.Data;
using WebOfWork.Api.DTOs.Payouts;
using WebOfWork.Api.DTOs.Payments;
using WebOfWork.Api.Models;
using WebOfWork.Api.Models.Enums;
using WebOfWork.Api.Integrations.PayPal;
using WebOfWork.Api.Integrations.Sms;
using WebOfWork.Api.Integrations.Sms.Strategies;

namespace WebOfWork.Api.Services;

public interface IPayoutService
{
    Task<List<PaymentResponse>> GetPendingPayoutsAsync(CancellationToken ct);
    Task<bool> ReleasePayoutAsync(ReleasePayoutRequest request, CancellationToken ct);
}

public class PayoutService : IPayoutService
{
    private readonly WebOfWorkDbContext _db;
    private readonly IPayPalPayoutClient _payPalPayoutClient;
    private readonly ISmsSender _smsSender;
    private readonly SmsStrategyFactory _smsFactory;

    public PayoutService(WebOfWorkDbContext db, IPayPalPayoutClient payPalPayoutClient, ISmsSender smsSender)
    {
        _db = db;
        _payPalPayoutClient = payPalPayoutClient;
        _smsSender = smsSender;
        _smsFactory = new SmsStrategyFactory();
    }

    public async Task<List<PaymentResponse>> GetPendingPayoutsAsync(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var payouts = await _db.Payments
            .Include(p => p.Contract)
            .Where(p => p.Status == PaymentStatus.Escrow 
                     && p.Contract.Status == ContractStatus.Completed 
                     && p.Contract.PayoutEligibleAfter.HasValue 
                     && p.Contract.PayoutEligibleAfter.Value <= now)
            .OrderBy(p => p.CreatedAt)
            .ToListAsync(ct);

        return payouts.Select(p => new PaymentResponse
        {
            Id = p.Id,
            ContractId = p.ContractId,
            MilestoneId = p.MilestoneId,
            PayPalOrderId = p.PayPalOrderId,
            Amount = p.Amount - p.PlatformFee,
            Status = p.Status.ToString(),
            CreatedAt = p.CreatedAt
        }).ToList();
    }

    public async Task<bool> ReleasePayoutAsync(ReleasePayoutRequest request, CancellationToken ct)
    {
        var payment = await _db.Payments
            .Include(p => p.Contract)
                .ThenInclude(c => c.Freelancer)
                    .ThenInclude(f => f.User)
            .FirstOrDefaultAsync(p => p.Id == request.PaymentId, ct);

        if (payment == null) throw new InvalidOperationException("Payment not found.");
        if (payment.Status == PaymentStatus.Released) throw new InvalidOperationException("Payout already released.");
        if (payment.Status != PaymentStatus.Escrow) throw new InvalidOperationException("Payment is not in Escrow.");
        
        // 13.5: Prevent payout before contract completion
        if (payment.Contract.Status != ContractStatus.Completed) throw new InvalidOperationException("Contract is not completed.");
        
        // 13.5a: Enforce payout eligibility window
        if (!payment.Contract.PayoutEligibleAfter.HasValue || payment.Contract.PayoutEligibleAfter.Value > DateTime.UtcNow) 
            throw new InvalidOperationException("Payout is not yet eligible.");

        var netAmount = payment.Amount - payment.PlatformFee;
        var note = $"Payout for Contract #{payment.ContractId}";
        
        var email = payment.Contract.Freelancer.User.Email; 

        var success = await _payPalPayoutClient.SendPayoutAsync(email, netAmount, note);
        if (!success) throw new InvalidOperationException("PayPal payout failed.");

        payment.Status = PaymentStatus.Released;
        payment.ProcessedAt = DateTime.UtcNow;
        payment.Contract.PaidToFreelancer = true;
        
        await _db.SaveChangesAsync(ct);

        if (payment.Contract.Freelancer.IsPhoneVerified && !string.IsNullOrEmpty(payment.Contract.Freelancer.Phone))
        {
            var msg = _smsFactory.GetStrategy("payout_released").GenerateMessage(netAmount, payment.ContractId);
            await _smsSender.SendSmsAsync(payment.Contract.Freelancer.Phone, msg);
        }

        return true;
    }
}
