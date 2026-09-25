using System.Threading;
using System.Threading.Tasks;
using System.IO;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebOfWork.Api.DTOs.Payments;
using WebOfWork.Api.Helpers;
using WebOfWork.Api.Models.Enums;
using System.Security.Claims;
using Microsoft.Extensions.Logging;
using WebOfWork.Api.Integrations.PayPal;
using WebOfWork.Api.Services;

namespace WebOfWork.Api.Controllers;

[ApiController]
[Route("api/payments")]
[Authorize]
public class PaymentsController : ControllerBase
{
    private readonly IPaymentService _paymentService;
    private readonly IPayPalWebhookVerifier _payPalWebhookVerifier;
    private readonly IPayPalWebhookHandler _webhookHandler;
    private readonly ILogger<PaymentsController> _logger;

    public PaymentsController(
        IPaymentService paymentService,
        IPayPalWebhookVerifier payPalWebhookVerifier,
        IPayPalWebhookHandler webhookHandler,
        ILogger<PaymentsController> logger)
    {
        _paymentService = paymentService;
        _payPalWebhookVerifier = payPalWebhookVerifier;
        _webhookHandler = webhookHandler;
        _logger = logger;
    }

    [HttpPost("verify")]
    [Authorize(Roles = "Client")]
    public async Task<IActionResult> VerifyPayment([FromBody] VerifyPaymentRequest request, CancellationToken cancellationToken)
    {
        var result = await _paymentService.VerifyPaymentAsync(Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!), request, cancellationToken);
        return Ok(result);
    }

    [HttpPost("verify-payment")]
    [Authorize(Roles = "Client")]
    public async Task<IActionResult> VerifyPaymentAndCreateContract([FromBody] VerifyPaymentRequest request, CancellationToken ct)
    {
        try { return StatusCode(201, await _paymentService.VerifyPaymentAndCreateContractAsync(Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!), request, ct)); }
        catch (ArgumentException e) { return BadRequest(new { error = e.Message }); }
        catch (KeyNotFoundException e) { return NotFound(new { error = e.Message }); }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }

    /// <summary>
    /// Was unroutable (no HttpGet attribute) and returned only { total_spending_usd }.
    /// SpendingSummary.jsx expects total_spent / projects_completed / ongoing_projects /
    /// avg_per_project / recent_projects, so this now returns the full the reference shape.
    /// </summary>
    // The controller route is "api/payments", so "client/spending-summary" and the absolute
    // "~/api/payments/client/spending-summary" resolve identically — a single attribute avoids
    // the "request matched multiple endpoints" 500.
    [HttpGet("client/spending-summary")]
    [Authorize(Roles = "Client")]
    public async Task<IActionResult> GetClientSpending(CancellationToken cancellationToken)
    {
        var summary = await _paymentService.GetClientSpendingDetailsAsync(Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!), cancellationToken);
        return Ok(summary);
    }

    [HttpGet("freelancer/history")]
    [HttpGet("~/api/payments/freelancer/transaction-history")]
    [Authorize(Roles = "Freelancer")]
    public async Task<IActionResult> GetFreelancerHistory(
        [FromQuery] string? status,
        [FromQuery] string? range,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        // The reference shape: paged envelope whose `results` is an object holding
        // total_earning / total_projects / contracts. The flat PaymentResponse
        // list remains available on the original freelancer/history route.
        var earnings = await _paymentService.GetFreelancerEarningsAsync(
            Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!),
            status, range, page, pageSize, cancellationToken);
        return Ok(earnings);
    }

    [HttpGet("~/api/payments/freelancer/earnings")]
    [Authorize(Roles = "Freelancer")]
    public async Task<IActionResult> GetFreelancerEarnings(
        [FromQuery] string? status,
        [FromQuery] string? range,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var earnings = await _paymentService.GetFreelancerEarningsAsync(
            Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!),
            status, range, page, pageSize, cancellationToken);
        return Ok(earnings);
    }

    [HttpGet("admin/transactions")]
    [Authorize(Policy = "RequireAdmin")]
    public async Task<IActionResult> GetAdminTransactions(CancellationToken cancellationToken)
    {
        var transactions = await _paymentService.GetPlatformTransactionsAsync(cancellationToken);
        return Ok(transactions);
    }

    // /payments/platform-transactions/ needs the admin projection
    // (payment_id, client_name, freelancer_name, amount_total, date) plus DRF paging.
    [HttpGet("~/api/payments/platform-transactions")]
    [Authorize(Policy = "RequireAdmin")]
    public async Task<IActionResult> GetPagedAdminTransactions([FromQuery] string? status, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] int? page_size = null, CancellationToken cancellationToken = default)
    {
        PaymentStatus? statusFilter = null;
        if (!string.IsNullOrWhiteSpace(status) && EnumQueryParser.TryParse<PaymentStatus>(status, out var parsed)) statusFilter = parsed;
        // This API spells the page size both ways: CategoriesController and
        // ProjectsController take `page_size`, the rest take `pageSize`.
        // Accepting only one means the other spelling is silently dropped and the
        // caller gets 20 rows with no indication why.
        var size = Math.Clamp(page_size ?? pageSize, 1, 100);
        var result = await _paymentService.GetPagedAdminTransactionsAsync(statusFilter, page, size, cancellationToken);
        PaginationLinks.Apply(Request, result.Page, result.PageSize, result.Count, v => result.Next = v, v => result.Previous = v);
        return Ok(result);
    }

    [HttpPost("webhook")]
    [AllowAnonymous]
    public async Task<IActionResult> HandlePayPalWebhook(CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(Request.Body);
        var rawBody = await reader.ReadToEndAsync(cancellationToken);
        var verified = await _payPalWebhookVerifier.VerifyAsync(Request.Headers, rawBody, cancellationToken);
        if (!verified)
            return BadRequest(new { message = "Invalid PayPal webhook signature." });

        try
        {
            using var document = System.Text.Json.JsonDocument.Parse(rawBody);
            var root = document.RootElement;
            var eventType = root.TryGetProperty("event_type", out var eventElement) ? eventElement.GetString() : "unknown";
            var resourceId = root.TryGetProperty("resource", out var resource) && resource.TryGetProperty("id", out var id) ? id.GetString() : null;
            var result = await _webhookHandler.HandleAsync(root, cancellationToken);
            _logger.LogInformation("Verified PayPal webhook {EventType} for resource {ResourceId}; outcome {Outcome}", eventType, resourceId, result.Outcome);
            return Ok(new { received = true, eventType, resourceId, outcome = result.Outcome });
        }
        catch (System.Text.Json.JsonException ex)
        {
            _logger.LogWarning(ex, "PayPal webhook body was not valid JSON.");
            return BadRequest(new { message = "Invalid PayPal webhook payload." });
        }
    }
}

