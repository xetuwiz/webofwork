using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebOfWork.Api.DTOs.Milestones;
using WebOfWork.Api.Services;

namespace WebOfWork.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class MilestonesController : ControllerBase
{
    private readonly IMilestoneService _milestoneService;
    private readonly ICurrentUser _currentUser;

    public MilestonesController(IMilestoneService milestoneService, ICurrentUser currentUser)
    {
        _milestoneService = milestoneService;
        _currentUser = currentUser;
    }

    [HttpGet("contract/{contractId}")]
    public async Task<IActionResult> GetMilestones(int contractId)
    {
        try
        {
            var milestones = await _milestoneService.GetMilestonesAsync(_currentUser.Id!.Value, contractId);
            return Ok(milestones);
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }

    [HttpPost("contract/{contractId}")]
    [Authorize(Policy = "RequireClient")]
    public async Task<IActionResult> CreateMilestones(int contractId, [FromBody] List<CreateMilestoneRequest> requests)
    {
        try
        {
            var milestones = await _milestoneService.CreateMilestonesAsync(_currentUser.Id!.Value, contractId, requests);
            return Ok(milestones);
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPost("{id}/start")]
    [Authorize(Policy = "RequireClient")]
    public async Task<IActionResult> StartMilestone(int id)
    {
        try
        {
            var success = await _milestoneService.StartMilestoneAsync(_currentUser.Id!.Value, id);
            if (!success) return NotFound();
            return NoContent();
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPost("{id}/submit")]
    [Authorize(Policy = "RequireFreelancer")]
    public async Task<IActionResult> SubmitMilestone(int id, [FromBody] SubmitMilestoneRequest request)
    {
        try
        {
            var success = await _milestoneService.SubmitMilestoneAsync(_currentUser.Id!.Value, id, request);
            if (!success) return NotFound();
            return NoContent();
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPost("{id}/approve")]
    [Authorize(Policy = "RequireClient")]
    public async Task<IActionResult> ApproveMilestone(int id)
    {
        try
        {
            var success = await _milestoneService.ApproveMilestoneAsync(_currentUser.Id!.Value, id);
            if (!success) return NotFound();
            return NoContent();
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPost("{id}/request-revision")]
    [Authorize(Policy = "RequireClient")]
    public async Task<IActionResult> RequestRevision(int id)
    {
        try
        {
            var success = await _milestoneService.RequestRevisionAsync(_currentUser.Id!.Value, id);
            if (!success) return NotFound();
            return NoContent();
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }
}
