using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FluentValidation;
using WebOfWork.Api.DTOs.Invitations;
using WebOfWork.Api.Helpers;
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
    [HttpPost("~/api/projects/invitations")]
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
    [HttpGet("~/api/projects/invitations/sent")]
    [Authorize(Policy = "RequireClient")]
    public async Task<IActionResult> GetSentInvitations([FromQuery] string? status, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] int? project_id = null)
    {
        InvitationStatus? statusFilter = EnumQueryParser.TryParse<InvitationStatus>(status, out var parsed) ? parsed : null;
        var result = await _invitationService.GetPagedSentInvitationsAsync(_currentUser.Id!.Value, statusFilter, page, pageSize, project_id);
        PaginationLinks.Apply(Request, result.Page, result.PageSize, result.Count, v => result.Next = v, v => result.Previous = v);
        return Ok(result);
    }

    [HttpGet("received")]
    [HttpGet("~/api/projects/invitations/received")]
    [Authorize(Policy = "RequireFreelancer")]
    public async Task<IActionResult> GetReceivedInvitations([FromQuery] string? status, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] int? page_size = null)
    {
        // "closed" is a UI grouping over two real statuses (declined + cancelled).
        // The enum has no such member, so it is flagged here and expanded in the
        // service, which owns the query.
        //
        // Before this, the client sent `status=hired`, which matched no enum
        // member, so `TryParse` returned false and the filter became null -- the
        // endpoint then returned EVERY invitation under a "hired" tab label. An
        // unrecognised value silently meaning "no filter" is a trap worth naming.
        var closedOnly = string.Equals(status?.Trim(), "closed", StringComparison.OrdinalIgnoreCase);
        InvitationStatus? statusFilter = null;
        if (!closedOnly && EnumQueryParser.TryParse<InvitationStatus>(status, out var parsed))
        {
            statusFilter = parsed;
        }

        // This API spells the page size both ways; accepting only one means the
        // other is silently dropped and the caller gets 20 rows with no hint why.
        var size = Math.Clamp(page_size ?? pageSize, 1, 100);
        var result = await _invitationService.GetPagedReceivedInvitationsAsync(_currentUser.Id!.Value, statusFilter, page, size, closedOnly);
        PaginationLinks.Apply(Request, result.Page, result.PageSize, result.Count, v => result.Next = v, v => result.Previous = v);
        return Ok(result);
    }

    [HttpPatch("{id}/status")]
    [HttpPatch("~/api/projects/invitations/{id}/update-status")]
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
    [HttpPatch("~/api/projects/invitations/{id}/cancel")]
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
    [HttpGet("~/api/projects/invitations/my-eligible-projects")]
    [Authorize(Policy = "RequireClient")]
    public async Task<IActionResult> GetEligibleProjects()
    {
        var projects = await _invitationService.GetEligibleProjectsAsync(_currentUser.Id!.Value);
        return Ok(projects.Select(p => new { p.Id, p.Title }));
    }
}
