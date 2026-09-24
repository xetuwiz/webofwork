using System.Text.Json;
using WebOfWork.Api.Data;
using WebOfWork.Api.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace WebOfWork.Api.Integrations.PayPal;

public interface IPayPalWebhookHandler
{
    Task<PayPalWebhookResult> HandleAsync(JsonElement webhookEvent, CancellationToken ct = default);
}

public sealed record PayPalWebhookResult(string EventType, string ResourceId, string Outcome);

public sealed class PayPalWebhookHandler : IPayPalWebhookHandler
{
    private readonly WebOfWorkDbContext _db;
    private readonly IPayPalClient _payPalClient;
    private readonly ILogger<PayPalWebhookHandler> _logger;

    public PayPalWebhookHandler(WebOfWorkDbContext db, IPayPalClient payPalClient, ILogger<PayPalWebhookHandler> logger)
    {
        _db = db;
        _payPalClient = payPalClient;
        _logger = logger;
    }

    public async Task<PayPalWebhookResult> HandleAsync(JsonElement webhookEvent, CancellationToken ct = default)
    {
        var eventType = webhookEvent.TryGetProperty("event_type", out var eventElement) ? eventElement.GetString() ?? "unknown" : "unknown";
        var resourceId = webhookEvent.TryGetProperty("resource", out var resource) && resource.TryGetProperty("id", out var id) ? id.GetString() ?? string.Empty : string.Empty;
        if (string.IsNullOrWhiteSpace(resourceId)) return new PayPalWebhookResult(eventType, resourceId, "ignored");

        if (eventType is not ("PAYMENT.CAPTURE.COMPLETED" or "PAYMENT.CAPTURE.REFUNDED"))
            return new PayPalWebhookResult(eventType, resourceId, "ignored");

        // PayPal sends the capture/refund resource ID, not always the order ID. Reconcile only
        // when the local ledger already has a matching order; never invent a contract/payment.
        var payment = await _db.Payments.AsNoTracking()
            .FirstOrDefaultAsync(p => p.PayPalOrderId == resourceId || p.Refunds.Any(r => r.Reason.Contains(resourceId)), ct);
        if (payment is null)
        {
            _logger.LogInformation("PayPal webhook {EventType} resource {ResourceId} has no local ledger match; no mutation made.", eventType, resourceId);
            return new PayPalWebhookResult(eventType, resourceId, "unmatched");
        }

        var order = await _payPalClient.GetOrderDetailsAsync(payment.PayPalOrderId);
        if (order is null || !string.Equals(order.Status, "COMPLETED", StringComparison.OrdinalIgnoreCase))
            return new PayPalWebhookResult(eventType, resourceId, "unverified");

        if (eventType == "PAYMENT.CAPTURE.COMPLETED" && payment.Status == PaymentStatus.Escrow)
            return new PayPalWebhookResult(eventType, resourceId, "already_reconciled");

        _logger.LogInformation("PayPal webhook {EventType} reconciled for payment {PaymentId}; status={Status}, amount={Amount}, currency={Currency}", eventType, payment.Id, payment.Status, order.Amount, order.Currency);
        return new PayPalWebhookResult(eventType, resourceId, "reconciled");
    }
}
