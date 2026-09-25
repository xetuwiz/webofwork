using System;
using WebOfWork.Api.Helpers;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebOfWork.Api.DTOs.Contracts;
using WebOfWork.Api.Models.Enums;
using WebOfWork.Api.Services;

namespace WebOfWork.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ContractsController : ControllerBase
{
    private readonly IContractService _contractService;
    private readonly ICurrentUser _currentUser;

    public ContractsController(IContractService contractService, ICurrentUser currentUser)
    {
        _contractService = contractService;
        _currentUser = currentUser;
    }

    [HttpPost]
    [AllowAnonymous]
    public IActionResult CreateContract() => StatusCode(405, new { error = "Use payment flow to create contracts" });

    /// <param name="page">1-based page index. This was accepted but never applied --
    /// the action returned every contract and hardcoded next/previous to null, so
    /// both Contracts.jsx and ClientContracts.jsx (both gate on data?.next) showed a
    /// paginator whose Next button could never be pressed.</param>
    [HttpGet]
    public async Task<IActionResult> GetContracts([FromQuery] string? search, [FromQuery] string? status, [FromQuery] string? role, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] int? page_size = null)
    {
        WebOfWork.Api.Models.Enums.ContractStatus? statusFilter = Enum.TryParse<WebOfWork.Api.Models.Enums.ContractStatus>(status, true, out var parsed) ? parsed : null;
        var all = await _contractService.GetContractsAsync(_currentUser.Id!.Value, search, statusFilter, role);

        var p = Math.Max(page, 1);
        // Both spellings accepted, matching the other list endpoints.
        var size = Math.Clamp(page_size ?? pageSize, 1, 100);
        var items = all.Skip((p - 1) * size).Take(size).ToList();

        // ClientContracts.jsx reads data?.results / .count / .next / .previous, so a bare array renders an empty list with dead pagination.
        var result = new PagedListResponse<ContractResponse>
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

    [HttpGet("{id}")]
    public async Task<IActionResult> GetContractDetail(int id)
    {
        try
        {
            var contract = await _contractService.GetContractDetailAsync(_currentUser.Id!.Value, id);
            if (contract == null) return NotFound();
            return Ok(contract);
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }

    /// <remarks>
    /// Was returning the whole list as a bare array and ignoring <c>?page=</c>.
    /// <c>FreelancerOffers.jsx</c> guarded its paginator on
    /// <c>offers.count &gt; 10</c> and enabled its arrows from
    /// <c>offers?.next</c> / <c>?.previous</c>. On an array those are always
    /// undefined, so the guard was never true and the footer never rendered at
    /// all -- the second page was unreachable. There was nothing on the wire for
    /// the client to read.
    /// <para>
    /// Sliced here rather than in the service because the rows are already
    /// materialised by <c>GetMyOffersAsync</c>; this leaves that method and its
    /// interface untouched for any other caller.
    /// </para>
    /// </remarks>
    [HttpGet("my_offers")]
    [Authorize(Policy = "RequireFreelancer")]
    public async Task<IActionResult> GetMyOffers([FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] int? page_size = null)
    {
        var all = await _contractService.GetMyOffersAsync(_currentUser.Id!.Value);
        var p = Math.Max(page, 1);
        var size = Math.Clamp(page_size ?? pageSize, 1, 100);

        var result = new PagedListResponse<ContractResponse>
        {
            Count = all.Count,
            Page = p,
            PageSize = size,
            Results = all.Skip((p - 1) * size).Take(size).ToList(),
        };

        PaginationLinks.Apply(Request, result.Page, result.PageSize, result.Count, v => result.Next = v, v => result.Previous = v);
        return Ok(result);
    }

    [HttpPost("{id}/accept")]
    [Authorize(Policy = "RequireFreelancer")]
    public async Task<IActionResult> AcceptContract(int id, [FromBody] AcceptContractRequest request)
    {
        // `AcceptContractAsync` copies `request.Signature` straight onto
        // `contract.FreelancerSignature` and flips the contract to Active with
        // no check of its own. A body whose keys do not bind -- which is what
        // happens the moment a client sends `Signature`/`SignatureType` instead
        // of `signature`/`signature_type`, because `Program.cs` sets
        // `PropertyNamingPolicy = SnakeCaseLower` for deserialisation too --
        // therefore recorded a legally empty acceptance and activated the
        // contract. Reject it here instead.
        if (string.IsNullOrWhiteSpace(request.Signature))
        {
            return BadRequest(new { error = "A signature is required to accept this contract." });
        }
        if (string.IsNullOrWhiteSpace(request.SignatureType))
        {
            return BadRequest(new { error = "signature_type is required (e.g. \"type\" or \"draw\")." });
        }

        try
        {
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "0.0.0.0";
            var success = await _contractService.AcceptContractAsync(_currentUser.Id!.Value, id, request, ip);
            if (!success) return NotFound();
            return NoContent();
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    /// PATCH /api/contracts/{id}/status  (and the {id}/status_update alias)
    ///
    /// Was `[Authorize(Policy = "RequireClient")]`, which made it impossible for
    /// the freelancer to use. That contradicted the service underneath it:
    /// `ContractService.UpdateStatusAsync` looks up **both** the caller's client
    /// and freelancer profile and then throws
    /// `UnauthorizedAccessException("You are not a participant in this
    /// contract.")` unless the caller is one of the two -- i.e. it was written
    /// to serve both parties, and the policy simply stopped freelancers from
    /// reaching it.
    ///
    /// That matters because the state machine lets either side do this:
    /// `Offered -> {Active, Cancelled}`, `Active -> {Submitted, Cancelled,
    /// Disputed}`, `Submitted -> {Completed, Active, Disputed}`. So the
    /// freelancer's "Cancel contract" and "Raise a dispute" actions are exactly
    /// this route, and both were unreachable (403). The policy is dropped and
    /// the service's own participation check governs.
    [HttpPatch("{id}/status")]
    [HttpPatch("{id}/status_update")]
    [Authorize]
    public async Task<IActionResult> UpdateStatusParity(int id, [FromBody] UpdateContractStatusRequest request) => await UpdateStatus(id, request);

    /// <summary>
    /// The status-change implementation, shared by the routed parity action above
    /// and by <c>ContractTests</c>.
    /// </summary>
    /// <remarks>
    /// <c>[NonAction]</c> is load-bearing, not decoration. This is a public method
    /// on a controller with no HTTP attribute, so ASP.NET does not route it — but
    /// Swashbuckle's API-description provider *does* pick it up as an action with
    /// no HTTP method, and the whole Swagger document then fails to generate:
    ///
    ///   SwaggerGeneratorException: Ambiguous HTTP method for action -
    ///   ContractsController.UpdateStatus. Actions require an explicit HttpMethod
    ///   binding for Swagger/OpenAPI 3.0
    ///
    /// which surfaced as a 500 on <c>/swagger/v1/swagger.json</c> — the API
    /// documentation was unreachable because of one shared helper method.
    /// <c>NonAction</c> is the documented way to keep a public helper visible to
    /// tests without letting the API explorer treat it as an endpoint.
    /// </remarks>
    [NonAction]
    public async Task<IActionResult> UpdateStatus(int id, [FromBody] UpdateContractStatusRequest request)
    {
        // A missing/unknown status binds to the default enum member, which is
        // `Offered` (ordinal 0). Left unchecked that would turn a typo'd body
        // into a real "reset this contract to Offered" write.
        if (!Enum.IsDefined(typeof(ContractStatus), request.Status))
        {
            return BadRequest(new
            {
                error = "Unknown contract status.",
                allowed = EnumNaming.AllowedWireValues<ContractStatus>()
            });
        }

        try
        {
            var success = await _contractService.UpdateStatusAsync(_currentUser.Id!.Value, id, request.Status);
            if (!success) return NotFound();
            return NoContent();
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
    }

}

