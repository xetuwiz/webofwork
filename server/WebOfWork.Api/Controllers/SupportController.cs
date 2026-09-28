using System;
using WebOfWork.Api.Helpers;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebOfWork.Api.Authorization;
using WebOfWork.Api.DTOs.Support;
using WebOfWork.Api.Models;
using WebOfWork.Api.Models.Enums;
using WebOfWork.Api.Services;

namespace WebOfWork.Api.Controllers;

[ApiController]
[Route("api/support")]
[Authorize]
public class SupportController : ControllerBase
{
    private readonly ISupportService _support;
    private readonly IValidator<CreateSupportTicketRequest> _createValidator;
    private readonly IValidator<AdminReplyRequest> _replyValidator;
    private readonly ICurrentUser _currentUser;

    public SupportController(
        ISupportService support,
        IValidator<CreateSupportTicketRequest> createValidator,
        IValidator<AdminReplyRequest> replyValidator,
        ICurrentUser currentUser)
    {
        _support = support;
        _createValidator = createValidator;
        _replyValidator = replyValidator;
        _currentUser = currentUser;
    }

    [HttpPost("tickets")]
    public async Task<IActionResult> CreateTicket([FromBody] CreateSupportTicketRequest request, CancellationToken ct)
    {
        var v = await _createValidator.ValidateAsync(request, ct);
        if (!v.IsValid) return ValidationProblem(new ValidationProblemDetails(v.ToDictionary()));

        var userId = _currentUser.Id ?? Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var roleClaim = _currentUser.Role ?? User.FindFirstValue("role") ?? User.FindFirstValue(ClaimTypes.Role);
        if (!Enum.TryParse<UserRole>(roleClaim, true, out var role))
        {
            role = User.IsInRole("Freelancer") ? UserRole.Freelancer : UserRole.Client;
        }

        try
        {
            var result = await _support.CreateTicketAsync(userId, role, request, ct);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails
            {
                Status = StatusCodes.Status403Forbidden,
                Title = "Support ticket limit reached",
                Detail = ex.Message
            });
        }
    }

    [HttpGet("tickets")]
    public async Task<IActionResult> GetMyTickets([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var userId = _currentUser.Id ?? Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        return Ok(await _support.GetMyTicketsAsync(userId, page, pageSize, ct));
    }

    [HttpPatch("tickets/{id:int}/cancel")]
    public async Task<IActionResult> CancelMyTicket(int id, CancellationToken ct)
    {
        var userId = _currentUser.Id ?? Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        try
        {
            return Ok(await _support.CancelMyTicketAsync(userId, id, ct));
        }
        catch (KeyNotFoundException) { return NotFound($"Support ticket #{id} not found."); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (InvalidOperationException ex) { return BadRequest(new { Error = ex.Message }); }
    }

    [HttpGet("admin/tickets")]
    [Authorize(Policy = AuthorizationPolicies.RequireAdmin)]
    /// <param name="page_size">
    /// Alias for <paramref name="pageSize"/>. This API spells the page size both
    /// ways: CategoriesController and ProjectsController take <c>page_size</c>,
    /// the rest take <c>pageSize</c>. Accepting only one means the other spelling
    /// is silently dropped and the caller gets 20 rows with no indication why.
    /// </param>
    public async Task<IActionResult> AdminListTickets([FromQuery] string? status, [FromQuery] string? senderRole, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] int? page_size = null, CancellationToken ct = default)
    {
        SupportTicketStatus? s = null;
        if (!string.IsNullOrEmpty(status) && Enum.TryParse<SupportTicketStatus>(status, true, out var parsedStatus)) s = parsedStatus;
        UserRole? r = null;
        if (!string.IsNullOrEmpty(senderRole) && Enum.TryParse<UserRole>(senderRole, true, out var parsedRole)) r = parsedRole;
        var size = Math.Clamp(page_size ?? pageSize, 1, 100);
        return Ok(await _support.GetAdminTicketsAsync(s, r, page, size, ct));
    }

    [HttpGet("admin/tickets/{id:int}")]
    [Authorize(Policy = AuthorizationPolicies.RequireAdmin)]
    public async Task<IActionResult> AdminTicketDetail(int id, CancellationToken ct)
    {
        try { return Ok(await _support.GetAdminTicketDetailAsync(id, ct)); }
        catch (KeyNotFoundException) { return NotFound($"Support ticket #{id} not found."); }
    }

    [HttpPatch("admin/tickets/{id:int}/reply")]
    [Authorize(Policy = AuthorizationPolicies.RequireAdmin)]
    public async Task<IActionResult> AdminReply(int id, [FromBody] AdminReplyRequest request, CancellationToken ct)
    {
        var v = await _replyValidator.ValidateAsync(request, ct);
        if (!v.IsValid) return ValidationProblem(new ValidationProblemDetails(v.ToDictionary()));

        var adminUserId = _currentUser.Id ?? Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        try { return Ok(await _support.AdminReplyAsync(id, adminUserId, request, ct)); }
        catch (KeyNotFoundException) { return NotFound($"Support ticket #{id} not found."); }
        catch (InvalidOperationException ex) { return BadRequest(new { Error = ex.Message }); }
    }
}
