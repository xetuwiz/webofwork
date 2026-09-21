using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebOfWork.Api.DTOs.Disputes;
using WebOfWork.Api.Services;

namespace WebOfWork.Api.Controllers;

[ApiController]
[Route("api/disputes")]
[Authorize]
public class DisputesController : ControllerBase
{
    private readonly IDisputeService _disputeService;

    public DisputesController(IDisputeService disputeService)
    {
        _disputeService = disputeService;
    }

    // Step 11.6: POST /api/disputes/contract/{contractId} — any participant
    [HttpPost("contract/{contractId:int}")]
    public async Task<IActionResult> Create(int contractId, [FromBody] CreateDisputeRequest request, CancellationToken cancellationToken)
    {
        var result = await _disputeService.CreateDisputeAsync(contractId, request, cancellationToken);
        return CreatedAtAction(nameof(GetParticipantDispute), new { contractId }, result);
    }

    // Step 11.11a: GET /api/disputes/contract/{contractId} — participant view own
    [HttpGet("contract/{contractId:int}")]
    public async Task<IActionResult> GetParticipantDispute(int contractId, CancellationToken cancellationToken)
    {
        var result = await _disputeService.GetParticipantDisputeAsync(contractId, cancellationToken);
        return Ok(result);
    }

    // Step 11.10: GET /api/disputes — admin only
    [HttpGet]
    [Authorize(Policy = "RequireAdmin")]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var result = await _disputeService.GetAdminDisputesAsync(cancellationToken);
        return Ok(result);
    }

    // Step 11.11: GET /api/disputes/{id} — admin only
    [HttpGet("{id:int}")]
    [Authorize(Policy = "RequireAdmin")]
    public async Task<IActionResult> GetDetail(int id, CancellationToken cancellationToken)
    {
        var result = await _disputeService.GetAdminDisputeDetailAsync(id, cancellationToken);
        return Ok(result);
    }

    // Step 11.13: POST /api/disputes/{id}/resolve — admin only
    [HttpPost("{id:int}/resolve")]
    [Authorize(Policy = "RequireAdmin")]
    public async Task<IActionResult> Resolve(int id, [FromBody] ResolveDisputeRequest request, CancellationToken cancellationToken)
    {
        var result = await _disputeService.ResolveDisputeAsync(id, request, cancellationToken);
        return Ok(result);
    }
}
