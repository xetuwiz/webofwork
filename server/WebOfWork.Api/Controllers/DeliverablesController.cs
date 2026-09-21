using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebOfWork.Api.DTOs.Deliverables;
using WebOfWork.Api.Services;

namespace WebOfWork.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DeliverablesController : ControllerBase
{
    private readonly IDeliverableService _deliverableService;
    private readonly ICurrentUser _currentUser;

    public DeliverablesController(IDeliverableService deliverableService, ICurrentUser currentUser)
    {
        _deliverableService = deliverableService;
        _currentUser = currentUser;
    }

    [HttpPost("contract/{contractId}")]
    [Authorize(Policy = WebOfWork.Api.Authorization.AuthorizationPolicies.RequireFreelancer)]
    public async Task<IActionResult> SubmitDeliverable(int contractId, [FromBody] SubmitDeliverableRequest request)
    {
        var result = await _deliverableService.SubmitDeliverableAsync(contractId, _currentUser.Id.Value, request);
        return Ok(result);
    }

    [HttpGet("contract/{contractId}")]
    public async Task<IActionResult> GetDeliverables(int contractId)
    {
        var result = await _deliverableService.GetDeliverablesAsync(contractId, _currentUser.Id.Value);
        return Ok(result);
    }
}
