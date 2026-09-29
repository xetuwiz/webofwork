using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FluentValidation;
using WebOfWork.Api.DTOs.Refunds;
using WebOfWork.Api.Services;

namespace WebOfWork.Api.Controllers;

[ApiController]
[Route("api/refunds")]
[Authorize(Policy = "RequireAdmin")]
public class RefundsController : ControllerBase
{
    private readonly IRefundService _refundService;
    private readonly IValidator<RefundPaymentRequest> _validator;

    public RefundsController(IRefundService refundService, IValidator<RefundPaymentRequest> validator)
    {
        _refundService = refundService;
        _validator = validator;
    }

    [HttpPost]
    [HttpPost("~/api/payments/refund-payment")]
    public async Task<IActionResult> RefundPayment([FromBody] RefundPaymentRequest request, CancellationToken cancellationToken)
    {
        var validation = await _validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid) return BadRequest(validation.Errors);
        var success = await _refundService.RefundPaymentAsync(request, cancellationToken);
        return Ok(new { Success = success });
    }
}
