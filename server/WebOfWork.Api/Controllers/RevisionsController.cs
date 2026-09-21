using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebOfWork.Api.DTOs.Revisions;
using WebOfWork.Api.Services;

namespace WebOfWork.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class RevisionsController : ControllerBase
{
    private readonly IRevisionService _revisionService;
    private readonly ICurrentUser _currentUser;

    public RevisionsController(IRevisionService revisionService, ICurrentUser currentUser)
    {
        _revisionService = revisionService;
        _currentUser = currentUser;
    }

    [HttpPost("contract/{contractId}")]
    [Authorize(Policy = WebOfWork.Api.Authorization.AuthorizationPolicies.RequireClient)]
    public async Task<IActionResult> RequestRevision(int contractId, [FromBody] RequestRevisionRequest request)
    {
        var result = await _revisionService.RequestRevisionAsync(contractId, _currentUser.Id.Value, request);
        return Ok(result);
    }

    [HttpPost("{id}/action")]
    [Authorize(Policy = WebOfWork.Api.Authorization.AuthorizationPolicies.RequireFreelancer)]
    public async Task<IActionResult> ActionRevision(int id, [FromBody] RevisionActionRequest request)
    {
        var result = await _revisionService.ActionRevisionAsync(id, _currentUser.Id.Value, request);
        return Ok(result);
    }

    [HttpGet("contract/{contractId}")]
    public async Task<IActionResult> GetRevisions(int contractId)
    {
        var result = await _revisionService.GetRevisionsAsync(contractId, _currentUser.Id.Value);
        return Ok(result);
    }
}
