using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FluentValidation;
using WebOfWork.Api.Helpers;
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

    /// POST /api/proposals
    ///
    /// Both create routes used to behave differently. This one validated and
    /// mapped exceptions; `create-proposal` below -- the path the web client
    /// actually calls -- had neither, so "you already proposed on this project",
    /// "that package is not yours" and "clients cannot submit proposals" all
    /// escaped as unhandled exceptions and surfaced as **500** with an opaque
    /// body. Both now share one implementation.
    [HttpPost]
    [Authorize(Policy = "RequireFreelancer")]
    public async Task<IActionResult> CreateProposal(
        [FromBody] CreateProposalRequest request,
        [FromServices] IValidator<CreateProposalRequest> validator)
    {
        var validationResult = await validator.ValidateAsync(request);
        if (!validationResult.IsValid) return BadRequest(new { error = "Invalid proposal.", details = validationResult.Errors });

        try
        {
            var created = await _proposalService.CreateProposalAsync(_currentUser.Id!.Value, request);
            // Was `CreatedAtAction(nameof(GetMyProposals), new { id = created.Id },
            // await GetMyProposalsAsync(...))` -- which pointed at an action that
            // declares no `id` route value and returned the caller's *entire*
            // proposal list as the created object. Clients reading the body got
            // an array where they expected one proposal.
            return CreatedAtAction(nameof(GetMyProposals), null, created);
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    /// POST /api/projects/create-proposal
    ///
    /// The route the web client posts to. Previously an expression-bodied
    /// one-liner with no validator and no exception handling, so every business
    /// rule in `ProposalService` turned into a 500.
    [HttpPost("~/api/projects/create-proposal")]
    [Authorize(Policy = "RequireFreelancer")]
    public async Task<IActionResult> CreateProjectProposal(
        [FromBody] CreateProposalRequest request,
        [FromServices] IValidator<CreateProposalRequest> validator)
    {
        var validationResult = await validator.ValidateAsync(request);
        if (!validationResult.IsValid) return BadRequest(new { error = "Invalid proposal.", details = validationResult.Errors });

        try
        {
            return Ok(await _proposalService.CreateProposalAsync(_currentUser.Id!.Value, request));
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    [HttpGet("my")]
    [HttpGet("~/api/projects/my-proposals")]
    [Authorize(Policy = "RequireFreelancer")]
    public async Task<IActionResult> GetMyProposals(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 10, [FromQuery] string? status = null, [FromQuery] int? page_size = null)
    {
        var all = await _proposalService.GetMyProposalsAsync(_currentUser.Id!.Value);

        // The page and status filters were accepted on the client but never read
        // here: the endpoint returned the whole list as a bare array, so MyProposals'
        // data.count was undefined and its pagination block never rendered.
        IEnumerable<object> filtered = all;
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (Enum.TryParse<WebOfWork.Api.Models.Enums.ProposalStatus>(status, true, out var parsed))
                filtered = all.Where(p => p.Status == parsed);
        }

        var materialized = filtered.ToList();
        var count = materialized.Count;
        var safePage = page < 1 ? 1 : page;
        // This API spells the page size both ways; accepting only one means the
        // other is silently dropped and the caller silently gets 10 rows.
        var requestedSize = page_size ?? pageSize;
        var safeSize = requestedSize < 1 || requestedSize > 100 ? 10 : requestedSize;
        var items = materialized.Skip((safePage - 1) * safeSize).Take(safeSize).ToList();
        return Ok(new
        {
            count,
            next = safePage * safeSize < count ? $"?page={safePage + 1}&pageSize={safeSize}" : (string?)null,
            previous = safePage > 1 ? $"?page={safePage - 1}&pageSize={safeSize}" : (string?)null,
            results = items,
        });
    }

    [HttpGet("project/{projectId}")]
    [HttpGet("~/api/projects/{projectId:int}/proposals")]
    [Authorize(Policy = "RequireClient")]
    public async Task<IActionResult> GetProjectProposals(int projectId, [FromQuery] string? status, [FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] int? page_size = null)
    {
        try
        {
            ProposalStatus? statusFilter = null;
            if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<WebOfWork.Api.Models.Enums.ProposalStatus>(status, true, out var parsedStatus)) statusFilter = parsedStatus;
            // This API spells the page size both ways; accepting only one means the
            // other is silently dropped and the caller gets 20 rows with no hint why.
            var size = Math.Clamp(page_size ?? pageSize, 1, 100);
            var proposals = await _proposalService.GetPagedProjectProposalsAsync(_currentUser.Id!.Value, projectId, statusFilter, search, page, size);
            PaginationLinks.Apply(Request, proposals.Page, proposals.PageSize, proposals.Count, v => proposals.Next = v, v => proposals.Previous = v);
            return Ok(proposals);
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }

    [HttpPatch("{id}/reject")]
    [HttpPatch("~/api/projects/proposals/{id}/reject")]
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
    [HttpDelete("~/api/projects/proposals/{id}")]
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
