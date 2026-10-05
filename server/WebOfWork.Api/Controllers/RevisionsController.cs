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
    [HttpPost("~/api/contracts/{contractId:int}/request-revision")]
    [Authorize(Policy = WebOfWork.Api.Authorization.AuthorizationPolicies.RequireClient)]
    public async Task<IActionResult> RequestRevision(int contractId, [FromBody] RequestRevisionRequest request)
    {
        if (_currentUser.Id is not Guid userId)
        {
            return Unauthorized();
        }

        try
        {
            var result = await _revisionService.RequestRevisionAsync(contractId, userId, request);
            return Ok(result);
        }
        catch (InvalidOperationException ex) { return Conflict(new { error = ex.Message }); }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
        catch (UnauthorizedAccessException ex) { return StatusCode(403, new { error = ex.Message }); }
    }

    [HttpPost("{id}/action")]
    [HttpPost("~/api/contracts/revisions/{id:int}/action")]
    [Authorize(Policy = WebOfWork.Api.Authorization.AuthorizationPolicies.RequireFreelancer)]
    public async Task<IActionResult> ActionRevision(int id, [FromBody] RevisionActionRequest request)
    {
        if (_currentUser.Id is not Guid userId)
        {
            return Unauthorized();
        }

        var result = await _revisionService.ActionRevisionAsync(id, userId, request);
        return Ok(result);
    }

    [HttpGet("contract/{contractId}")]
    [HttpGet("~/api/contracts/{contractId:int}/revisions")]
    public async Task<IActionResult> GetRevisions(int contractId)
    {
        if (_currentUser.Id is not Guid userId)
        {
            return Unauthorized();
        }

        var result = await _revisionService.GetRevisionsAsync(contractId, userId);
        return Ok(result);
    }
}
