using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebOfWork.Api.DTOs.Payouts;
using WebOfWork.Api.Services;

namespace WebOfWork.Api.Controllers;

[ApiController]
[Route("api/payouts")]
[Authorize]
public class PayoutsController : ControllerBase
{
    private readonly IPayoutService _payoutService;

    public PayoutsController(IPayoutService payoutService)
    {
        _payoutService = payoutService;
    }

    [HttpGet("pending")]
    [Authorize(Policy = "RequireAdmin")]
    public async Task<IActionResult> GetPendingPayouts(CancellationToken cancellationToken)
    {
        var payouts = await _payoutService.GetPendingPayoutsAsync(cancellationToken);
        return Ok(payouts);
    }

    [HttpPost("release")]
    [Authorize(Policy = "RequireAdmin")]
    public async Task<IActionResult> ReleasePayout([FromBody] ReleasePayoutRequest request, CancellationToken cancellationToken)
    {
        var success = await _payoutService.ReleasePayoutAsync(request, cancellationToken);
        return Ok(new { Success = success });
    }
}
