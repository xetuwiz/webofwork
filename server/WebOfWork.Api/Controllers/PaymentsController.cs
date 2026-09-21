using System.Threading;
using System.Threading.Tasks;
using System.IO;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebOfWork.Api.DTOs.Payments;
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
    private readonly ILogger<PaymentsController> _logger;

    public PaymentsController(
        IPaymentService paymentService,
        IPayPalWebhookVerifier payPalWebhookVerifier,
        ILogger<PaymentsController> logger)
    {
        _paymentService = paymentService;
        _payPalWebhookVerifier = payPalWebhookVerifier;
        _logger = logger;
    }

    [HttpPost("verify")]
    [Authorize(Roles = "Client")]
    public async Task<IActionResult> VerifyPayment([FromBody] VerifyPaymentRequest request, CancellationToken cancellationToken)
    {
        var result = await _paymentService.VerifyPaymentAsync(Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!), request, cancellationToken);
        return Ok(result);
    }

    [HttpGet("client/spending")]
    [Authorize(Roles = "Client")]
    public async Task<IActionResult> GetClientSpending(CancellationToken cancellationToken)
    {
        var total = await _paymentService.GetClientSpendingSummaryAsync(Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!), cancellationToken);
        return Ok(new { TotalSpendingUsd = total });
    }

    [HttpGet("freelancer/history")]
    [Authorize(Roles = "Freelancer")]
    public async Task<IActionResult> GetFreelancerHistory(CancellationToken cancellationToken)
    {
        var history = await _paymentService.GetFreelancerTransactionHistoryAsync(Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!), cancellationToken);
        return Ok(history);
    }

    [HttpGet("admin/transactions")]
    [Authorize(Policy = "RequireAdmin")]
    public async Task<IActionResult> GetAdminTransactions(CancellationToken cancellationToken)
    {
        var transactions = await _paymentService.GetPlatformTransactionsAsync(cancellationToken);
        return Ok(transactions);
    }

    [HttpPost("webhook")]
    [AllowAnonymous]
    public async Task<IActionResult> HandlePayPalWebhook(CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(Request.Body);
        var rawBody = await reader.ReadToEndAsync(cancellationToken);
        var verified = await _payPalWebhookVerifier.VerifyAsync(Request.Headers, rawBody, cancellationToken);
        if (!verified)
        {
            return BadRequest(new { message = "Invalid PayPal webhook signature." });
        }

        _logger.LogInformation("Verified PayPal webhook received: {TransmissionId}", Request.Headers["PAYPAL-TRANSMISSION-ID"].ToString());
        return Ok(new { received = true });
    }
}

