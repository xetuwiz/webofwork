using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebOfWork.Api.DTOs.Refunds;
using WebOfWork.Api.Services;

namespace WebOfWork.Api.Controllers;

[ApiController]
[Route("api/refunds")]
[Authorize(Policy = "RequireAdmin")]
public class RefundsController : ControllerBase
{
    private readonly IRefundService _refundService;

    public RefundsController(IRefundService refundService)
    {
        _refundService = refundService;
    }

    [HttpPost]
    public async Task<IActionResult> RefundPayment([FromBody] RefundPaymentRequest request, CancellationToken cancellationToken)
    {
        var success = await _refundService.RefundPaymentAsync(request, cancellationToken);
        return Ok(new { Success = success });
    }
}
