using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using WebOfWork.Api.Data;
using WebOfWork.Api.DTOs.Refunds;
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
            .FirstOrDefaultAsync(p => p.Id == request.PaymentId, ct);

        if (payment == null) throw new InvalidOperationException("Payment not found.");
        if (payment.Status == PaymentStatus.Refunded) throw new InvalidOperationException("Payment already refunded.");
        if (payment.Status != PaymentStatus.Escrow) throw new InvalidOperationException("Can only refund payments in Escrow.");

        var success = await _payPalRefundClient.RefundOrderAsync(payment.PayPalOrderId, payment.Amount, request.Reason);
        if (!success) throw new InvalidOperationException("PayPal refund failed.");

        using var transaction = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            payment.Status = PaymentStatus.Refunded;
            payment.ProcessedAt = DateTime.UtcNow;
            
            payment.Contract.Status = ContractStatus.Refunded;

            var analytics = await _db.PlatformAnalytics.FirstOrDefaultAsync(ct);
            if (analytics != null)
            {
                analytics.TotalVolumeUsd -= payment.Amount;
                analytics.TotalProfitUsd -= payment.PlatformFee;
            }

            await _db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }

        return true;
    }
}
