using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebOfWork.Api.DTOs.Disputes;
using WebOfWork.Api.Helpers;
using WebOfWork.Api.Models.Enums;
using WebOfWork.Api.Services;
using WebOfWork.Api.Validators.Disputes;

namespace WebOfWork.Api.Controllers;

[ApiController]
[Route("api/disputes")]
[Authorize]
public class DisputesController : ControllerBase
{
    private readonly IDisputeService _disputeService;
    // Both dispute actions take validated input. CreateDisputeValidator existed but was
    // never invoked, so the 10-character minimum on a description was not enforced.
    private readonly CreateDisputeValidator _createValidator;
    private readonly ResolveDisputeValidator _resolveValidator;

    public DisputesController(IDisputeService disputeService,
        CreateDisputeValidator createValidator,
        ResolveDisputeValidator resolveValidator)
    {
        _disputeService = disputeService;
        _createValidator = createValidator;
        _resolveValidator = resolveValidator;
    }

    /// The dispute-reason vocabulary, so clients render the same choices and labels
    /// the server accepts instead of hardcoding their own list.
    ///
    /// The web client's dropdown offered 4 of the 8 reasons, and its labels disagreed
    /// with mobile's. Same rationale as GET /api/notifications/preferences/types: the
    /// enum is the single source of truth for what is valid and what it is called.
    [HttpGet("reasons")]
    [AllowAnonymous]
    public IActionResult Reasons() => Ok(new { results = DisputeReasonCatalog.All });

    // Step 11.6: POST /api/disputes/contract/{contractId} — any participant
    [HttpPost("contract/{contractId:int}")]
    [HttpPost("~/api/contracts/dispute")]
    public async Task<IActionResult> Create(int contractId, [FromBody] CreateDisputeRequest request, CancellationToken cancellationToken)
    {
        // The the reference alias carries the id in the body (contract / contract_id) rather than in
        // the path, so fall back to those when the route parameter is absent.
        var v = await _createValidator.ValidateAsync(request, cancellationToken);
        if (!v.IsValid) return BadRequest(new { error = v.Errors.First().ErrorMessage });
        var resolvedContractId = contractId != 0 ? contractId
            : request.ContractId != 0 ? request.ContractId
            : request.Contract;
        var result = await _disputeService.CreateDisputeAsync(resolvedContractId, request, cancellationToken);
        return CreatedAtAction(nameof(GetParticipantDispute), new { contractId = resolvedContractId }, result);
    }

    // Step 11.11a: GET /api/disputes/contract/{contractId} — participant view own
    [HttpGet("contract/{contractId:int}")]
    public async Task<IActionResult> GetParticipantDispute(int contractId, CancellationToken cancellationToken)
    {
        var result = await _disputeService.GetParticipantDisputeAsync(contractId, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Admin dispute list.
    /// </summary>
    /// <param name="search">
    /// Matched case-insensitively against the case-insensitive columns available
    /// here: the dispute description and the reason. The client advertises
    /// "Search by ID, Client or Freelancer", but this action is not allowed to
    /// join the contract's client/freelancer names without pulling the whole
    /// graph in, so the placeholder was narrowed to match what is actually
    /// searched rather than leaving a promise the API does not keep.
    /// </param>
    /// <param name="status">
    /// Filter by status. Accepts the wire label ("pending", "resolved",
    /// "under_review", "rejected") or the numeric enum value.
    /// </param>
    /// <param name="page">1-based. Previously this endpoint took no parameters at
    /// all and returned a bare array, while DisputeList.jsx read
    /// <c>results</c>/<c>count</c>/<c>next</c>/<c>previous</c> -- so the page
    /// rendered an empty list with a permanently disabled paginator.</param>
    // Step 11.10: GET /api/disputes — admin only
    [HttpGet]
    [HttpGet("~/api/contracts/admin/disputes")]
    [Authorize(Policy = "RequireAdmin")]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? search,
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] int? page_size = null,
        CancellationToken cancellationToken = default)
    {
        DisputeStatus? statusFilter = null;
        if (!string.IsNullOrWhiteSpace(status)
            && EnumQueryParser.TryParse<DisputeStatus>(status, out var parsed))
        {
            statusFilter = parsed;
        }

        var all = await _disputeService.GetAdminDisputesAsync(cancellationToken);

        if (statusFilter.HasValue)
        {
            all = all.Where(d => d.Status == statusFilter.Value).ToList();
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            // Case-insensitive, because "Pending" and "pending" are both things a
            // user will type.
            var term = search.Trim();

            // The reason is compared on a normalised form. The wire value is
            // "poor_quality" (DisputeReasonJsonConverter) while the C# member is
            // "PoorQuality", so comparing against `Reason.ToString()` missed the
            // exact string the API shows the user -- searching for something you
            // can read on screen and getting nothing back. Stripping separators
            // makes poor_quality, "Poor Quality" and PoorQuality all match.
            static string Normalise(string value) =>
                new string(value.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();

            var needle = Normalise(term);
            all = all.Where(d =>
                d.Description.Contains(term, StringComparison.OrdinalIgnoreCase)
                || Normalise(d.Reason.ToString()).Contains(needle)
                || Normalise(d.Status.ToString()).Contains(needle)
                || d.Id.ToString() == term).ToList();
        }

        var p = Math.Max(page, 1);
        // Both spellings accepted: this API uses `page_size` on
        // CategoriesController/ProjectsController and `pageSize` elsewhere.
        var size = Math.Clamp(page_size ?? pageSize, 1, 100);
        var items = all.Skip((p - 1) * size).Take(size).ToList();

        var result = new PagedListResponse<DisputeResponse>
        {
            Count = all.Count,
            Page = p,
            PageSize = size,
            Results = items
        };
        PaginationLinks.Apply(Request, p, size, all.Count,
            v => result.Next = v, v => result.Previous = v);
        return Ok(result);
    }

    // Step 11.11: GET /api/disputes/{id} — admin only
    [HttpGet("{id:int}")]
    [HttpGet("~/api/contracts/admin/disputes/{id:int}")]
    [Authorize(Policy = "RequireAdmin")]
    public async Task<IActionResult> GetDetail(int id, CancellationToken cancellationToken)
    {
        var result = await _disputeService.GetAdminDisputeDetailAsync(id, cancellationToken);
        return Ok(result);
    }

    // Step 11.13: POST /api/disputes/{id}/resolve — admin only
    [HttpPost("{id:int}/resolve")]
    [HttpPost("~/api/contracts/admin/disputes/{id:int}/resolve")]
    [Authorize(Policy = "RequireAdmin")]
    public async Task<IActionResult> Resolve(int id, [FromBody] ResolveDisputeRequest request, CancellationToken cancellationToken)
    {
        // Validate BEFORE the service call. Running the write first meant a
        // request with a too-short admin note still returned 400 while the
        // dispute had already been resolved and the bad note persisted.
        var rv = await _resolveValidator.ValidateAsync(request, cancellationToken);
        if (!rv.IsValid) return BadRequest(new { error = rv.Errors.First().ErrorMessage });
        var result = await _disputeService.ResolveDisputeAsync(id, request, cancellationToken);
        return Ok(result);
    }
}
