using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FluentValidation;
using WebOfWork.Api.DTOs.Invitations;
using WebOfWork.Api.Models.Enums;
using WebOfWork.Api.Services;

namespace WebOfWork.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class InvitationsController : ControllerBase
{
    private readonly IInvitationService _invitationService;
    private readonly ICurrentUser _currentUser;

    public InvitationsController(IInvitationService invitationService, ICurrentUser currentUser)
    {
        _invitationService = invitationService;
        _currentUser       = currentUser;
    }

    [HttpPost]
    [Authorize(Policy = "RequireClient")]
    public async Task<IActionResult> CreateInvitation(
        [FromBody] CreateInvitationRequest request,
        [FromServices] IValidator<CreateInvitationRequest> validator)
    {
        var validationResult = await validator.ValidateAsync(request);
        if (!validationResult.IsValid) return BadRequest(validationResult.Errors);

        try
        {
            var response = await _invitationService.CreateInvitationAsync(_currentUser.Id!.Value, request);
            return CreatedAtAction(nameof(GetSentInvitations), new { id = response.Id }, response);
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
        catch (ArgumentException ex)         { return BadRequest(new { error = ex.Message }); }
    }

    [HttpGet("sent")]
    [Authorize(Policy = "RequireClient")]
    public async Task<IActionResult> GetSentInvitations([FromQuery] string? status, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        InvitationStatus? statusFilter = Enum.TryParse<InvitationStatus>(status, true, out var parsed) ? parsed : null;
        return Ok(await _invitationService.GetPagedSentInvitationsAsync(_currentUser.Id!.Value, statusFilter, page, pageSize));
    }

    [HttpGet("received")]
    [Authorize(Policy = "RequireFreelancer")]
    public async Task<IActionResult> GetReceivedInvitations([FromQuery] string? status, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        InvitationStatus? statusFilter = Enum.TryParse<InvitationStatus>(status, true, out var parsed) ? parsed : null;
        return Ok(await _invitationService.GetPagedReceivedInvitationsAsync(_currentUser.Id!.Value, statusFilter, page, pageSize));
    }

    [HttpPatch("{id}/status")]
    [Authorize(Policy = "RequireFreelancer")]
    public async Task<IActionResult> UpdateInvitationStatus(int id, [FromBody] UpdateInvitationStatusRequest request)
    {
        // Freelancers cannot cancel — only clients cancel via PATCH /{id}/cancel
        if (request.Status == InvitationStatus.Cancelled)
            return BadRequest(new { error = "Freelancers cannot cancel invitations. Use the client cancel endpoint." });

        try
        {
            var success = await _invitationService.UpdateInvitationStatusAsync(_currentUser.Id!.Value, id, request);
            if (!success) return NotFound();
            return NoContent();
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPatch("{id}/cancel")]
    [Authorize(Policy = "RequireClient")]
    public async Task<IActionResult> CancelInvitation(int id)
    {
        try
        {
            var success = await _invitationService.CancelInvitationAsync(_currentUser.Id!.Value, id);
            if (!success) return NotFound();
            return NoContent();
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpGet("eligible-projects")]
    [Authorize(Policy = "RequireClient")]
    public async Task<IActionResult> GetEligibleProjects()
    {
        var projects = await _invitationService.GetEligibleProjectsAsync(_currentUser.Id!.Value);
        return Ok(projects.Select(p => new { p.Id, p.Title }));
    }
}
