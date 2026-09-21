using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebOfWork.Api.DTOs.Payments;
using System.Security.Claims;
using WebOfWork.Api.Services;

namespace WebOfWork.Api.Controllers;

[ApiController]
[Route("api/payments")]
[Authorize]
public class PaymentsController : ControllerBase
{
    private readonly IPaymentService _paymentService;

    public PaymentsController(IPaymentService paymentService)
    {
        _paymentService = paymentService;
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
        return Ok(new { TotalSpendingLkr = total });
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
}

