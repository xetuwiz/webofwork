using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using WebOfWork.Api.Data;
using WebOfWork.Api.DTOs.Refunds;
using WebOfWork.Api.Models;
using WebOfWork.Api.Models.Enums;
using WebOfWork.Api.Integrations.PayPal;

namespace WebOfWork.Api.Services;

public interface IRefundService
{
    Task<bool> RefundPaymentAsync(RefundPaymentRequest request, CancellationToken ct);
}

public class RefundService : IRefundService
{
    private readonly WebOfWorkDbContext _db;
    private readonly IPayPalRefundClient _payPalRefundClient;

    public RefundService(WebOfWorkDbContext db, IPayPalRefundClient payPalRefundClient)
    {
        _db = db;
        _payPalRefundClient = payPalRefundClient;
    }

    public async Task<bool> RefundPaymentAsync(RefundPaymentRequest request, CancellationToken ct)
    {
        var payment = await _db.Payments
            .Include(p => p.Contract)
            .Include(p => p.Refunds)
            .FirstOrDefaultAsync(p => p.Id == request.PaymentId, ct);

        if (payment == null) throw new InvalidOperationException("Payment not found.");
        if (payment.Status == PaymentStatus.Refunded) throw new InvalidOperationException("Payment already refunded.");
        if (payment.Status != PaymentStatus.Escrow) throw new InvalidOperationException("Can only refund payments in Escrow.");

        var requestedAmount = request.Amount ?? payment.Amount - payment.RefundedAmount;
        if (requestedAmount <= 0m || requestedAmount > payment.Amount - payment.RefundedAmount)
            throw new InvalidOperationException("Refund amount exceeds the remaining refundable amount.");

        var success = await _payPalRefundClient.RefundOrderAsync(payment.PayPalOrderId, requestedAmount, request.Reason);
        if (!success) throw new InvalidOperationException("PayPal refund failed.");

        var existingTransaction = _db.Database.CurrentTransaction;
        var ownsTransaction = existingTransaction == null;
        var transaction = ownsTransaction
            ? await _db.Database.BeginTransactionAsync(ct)
            : null;
        try
        {
            payment.RefundedAmount += requestedAmount;
            payment.Status = payment.RefundedAmount >= payment.Amount ? PaymentStatus.Refunded : PaymentStatus.Escrow;
            payment.ProcessedAt = DateTime.UtcNow;
            _db.PaymentRefunds.Add(new PaymentRefund
            {
                PaymentId = payment.Id,
                Amount = requestedAmount,
                Reason = request.Reason,
                CreatedAt = DateTime.UtcNow
            });

            if (payment.Status == PaymentStatus.Refunded)
                payment.Contract.Status = ContractStatus.Refunded;

            var analytics = await _db.PlatformAnalytics.FirstOrDefaultAsync(ct);
            if (analytics != null)
            {
                analytics.TotalVolumeUsd -= requestedAmount;
                analytics.TotalProfitUsd -= Math.Min(payment.PlatformFee, requestedAmount);
            }

            await _db.SaveChangesAsync(ct);
            if (transaction != null) await transaction.CommitAsync(ct);
        }
        catch
        {
            if (transaction != null) await transaction.RollbackAsync(ct);
            throw;
        }

        return true;
    }
}
