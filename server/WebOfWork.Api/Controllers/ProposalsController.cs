using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FluentValidation;
using WebOfWork.Api.DTOs.Proposals;
using WebOfWork.Api.Models.Enums;
using WebOfWork.Api.Services;

namespace WebOfWork.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ProposalsController : ControllerBase
{
    private readonly IProposalService _proposalService;
    private readonly ICurrentUser _currentUser;

    public ProposalsController(IProposalService proposalService, ICurrentUser currentUser)
    {
        _proposalService = proposalService;
        _currentUser     = currentUser;
    }

    [HttpPost]
    [Authorize(Policy = "RequireFreelancer")]
    public async Task<IActionResult> CreateProposal(
        [FromBody] CreateProposalRequest request,
        [FromServices] IValidator<CreateProposalRequest> validator)
    {
        var validationResult = await validator.ValidateAsync(request);
        if (!validationResult.IsValid) return BadRequest(validationResult.Errors);

        try
        {
            var response = await _proposalService.CreateProposalAsync(_currentUser.Id!.Value, request);
            return CreatedAtAction(nameof(GetMyProposals), new { id = response.Id }, response);
        }
        catch (UnauthorizedAccessException ex) { return Forbid(); }
        catch (InvalidOperationException ex)   { return BadRequest(new { error = ex.Message }); }
        catch (ArgumentException ex)           { return BadRequest(new { error = ex.Message }); }
    }

    [HttpGet("my")]
    [Authorize(Policy = "RequireFreelancer")]
    public async Task<IActionResult> GetMyProposals()
    {
        var proposals = await _proposalService.GetMyProposalsAsync(_currentUser.Id!.Value);
        return Ok(proposals);
    }

    [HttpGet("project/{projectId}")]
    [Authorize(Policy = "RequireClient")]
    public async Task<IActionResult> GetProjectProposals(int projectId, [FromQuery] string? status, [FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        try
        {
            ProposalStatus? statusFilter = null;
            if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<WebOfWork.Api.Models.Enums.ProposalStatus>(status, true, out var parsedStatus)) statusFilter = parsedStatus;
            var proposals = await _proposalService.GetPagedProjectProposalsAsync(_currentUser.Id!.Value, projectId, statusFilter, search, page, pageSize);
            return Ok(proposals);
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }

    [HttpPatch("{id}/reject")]
    [Authorize(Policy = "RequireClient")]
    public async Task<IActionResult> RejectProposal(int id)
    {
        try
        {
            var success = await _proposalService.RejectProposalAsync(_currentUser.Id!.Value, id);
            if (!success) return NotFound();
            return NoContent();
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpDelete("{id}")]
    [Authorize(Policy = "RequireFreelancer")]
    public async Task<IActionResult> WithdrawProposal(int id)
    {
        try
        {
            var success = await _proposalService.WithdrawProposalAsync(_currentUser.Id!.Value, id);
            if (!success) return NotFound();
            return NoContent();
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }
}
