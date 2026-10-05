using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebOfWork.Api.Authorization;
using WebOfWork.Api.Services;
using Microsoft.EntityFrameworkCore;
using WebOfWork.Api.Data;
using WebOfWork.Api.Models;

namespace WebOfWork.Api.Controllers;

[ApiController]
[Route("api/ai/workflows")]
[Authorize]
public class AiWorkflowController : ControllerBase
{
    private readonly IAiWorkflowService _aiWorkflowService;
    private readonly ICurrentUser _currentUser;
    private readonly WebOfWorkDbContext _db;

    public AiWorkflowController(IAiWorkflowService aiWorkflowService, ICurrentUser currentUser, WebOfWorkDbContext db)
    {
        _aiWorkflowService = aiWorkflowService;
        _currentUser = currentUser;
        _db = db;
    }

    // The original single-filter list action was here. It is gone because the
    // richer one further down declares the same route, and two `[HttpGet]`
    // actions on one template is an `AmbiguousMatchException` on the *first
    // request* -- not a compile error, so it survived every build and every test
    // in this repository and would have surfaced as a 500 on the admin monitor.
    //
    // They are distinct C# overloads (4 parameters against 6), which is exactly
    // why the compiler was happy and the router was not. The overloads have
    // different parameter *names* too, so neither could have satisfied a call
    // that named the other's arguments.
    // Readable by the client who owns the run, and by an admin.
    //
    // This was `[RequireAdmin]` only, so a client opening
    // `/my-projects/{id}/ai-shortlist/{runId}` -- a page this project links to
    // from its own project detail -- got 403 and the screen read "Could not load
    // this run. It may have been cancelled, or the link may be wrong."
    //
    // Which is also self-contradictory: `ApproveShortlist` on the same controller
    // is `[RequireClient]`, so the client was authorised to *approve* a run they
    // were not allowed to *read*. A gate that refuses the reader while admitting
    // the actor is not a restriction, it is a mistake.
    //
    // The ownership filter below is what makes this safe, not the policy. An admin
    // does not satisfy `RequestedByUserId`, so an admin reading someone else's run
    // needs the query relaxed by role -- see `IsAdmin` in the branch below.
    [HttpGet("{workflowRunId:int}")]
    [Authorize(Policy = AuthorizationPolicies.RequireClientOrAdmin)]
    public async Task<IActionResult> GetWorkflow(int workflowRunId, CancellationToken cancellationToken)
    {
        var userId = _currentUser.Id ?? throw new UnauthorizedAccessException("Authenticated user is required.");

        // `ICurrentUser` exposes the role as a string rather than `IsInRole`, so
        // this is an equality check and not a claim about how the policy resolved.
        // The policy has already established the caller is a client or an admin;
        // this only decides whether the ownership filter applies.
        var isAdmin = string.Equals(
            _currentUser.Role, AuthorizationPolicies.RoleAdmin, StringComparison.OrdinalIgnoreCase);

        // Scoped by owner rather than trusting the route, so one client still
        // cannot read another's run. Returns 404 rather than 403 for a run that
        // exists but belongs to someone else: confirming it exists is itself a
        // leak, and 404 is what the ownership filter produces naturally.
        var query = _db.WorkflowRuns.AsNoTracking()
            .Include(x => x.Project)
            .Include(x => x.StepLogs)
            .Where(x => x.Id == workflowRunId);

        if (!isAdmin) query = query.Where(x => x.RequestedByUserId == userId);

        var run = await query.FirstOrDefaultAsync(cancellationToken);

        return run is null ? NotFound() : Ok(run);
    }

    [HttpGet("by-project/{projectId:int}")]
    [Authorize(Policy = AuthorizationPolicies.RequireClient)]
    public async Task<IActionResult> GetProjectWorkflowStatus(int projectId, CancellationToken cancellationToken)
    {
        var userId = _currentUser.Id ?? throw new UnauthorizedAccessException("Authenticated client user is required.");
        var run = await _db.WorkflowRuns.AsNoTracking()
            .Where(x => x.ProjectId == projectId && x.RequestedByUserId == userId)
            .OrderByDescending(x => x.CreatedAt)
            // `FinalOutput` is the agent's payload for this run, and the browser needs it.
//
// It was omitted, so the page could show a run's *status* but never anything the
// agent actually said. The clearest casualty: when the planner or the drafter
// stops to ask the client a clarifying question, that question is in the payload
// and there is no path by which it can reach the screen. The page said "needs
// clarification" and showed no question, leaving the client with nothing to
// answer.
.Select(x => new
{
    x.Id,
    x.ProjectId,
    x.Status,
    x.Objective,
    x.ApprovalDecision,
    x.ApprovalRationale,
    x.FinalOutput,
    x.CreatedAt,
    x.UpdatedAt,
})
            .FirstOrDefaultAsync(cancellationToken);
        return run is null ? NotFound() : Ok(run);
    }


    [HttpPost("start")]
    [Authorize(Policy = AuthorizationPolicies.RequireClient)]
    public async Task<IActionResult> StartWorkflow([FromBody] StartWorkflowRequest request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.Id ?? throw new UnauthorizedAccessException("Authenticated client user is required.");
        var run = await _aiWorkflowService.StartWorkflowAsync(
            userId, request.Objective, request.Stage, request.Plan, request.ProjectId,
            context: null, ct: cancellationToken);
        return Ok(run);
    }

    [HttpPost("{workflowRunId:int}/approve")]
    [Authorize(Policy = AuthorizationPolicies.RequireAdmin)]
    public async Task<IActionResult> ApproveWorkflow(int workflowRunId, [FromBody] ApproveWorkflowRequest? request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.Id ?? throw new UnauthorizedAccessException("Authenticated admin user is required.");
        var contractSpec = request?.FreelancerId is { } freelancerId && request.TotalAmount is { } totalAmount
            ? new WorkflowContractSpec(freelancerId, totalAmount, request.PackageId, request.ProjectId)
            : null;
        var run = await _aiWorkflowService.ApproveWorkflowAsync(workflowRunId, userId, request?.Decision, request?.Rationale, contractSpec, cancellationToken);
        return Ok(run);
    }

    /// <summary>
    /// Every run, for the admin monitor.
    /// </summary>
    /// <remarks>
    /// Paged rather than returning everything, because a busy platform accrues
    /// runs faster than anyone reads them, and an unpaged list on a table that
    /// only grows is a response that gets slower until it times out.
    ///
    /// Filterable by stage and status because the question an admin opens this
    /// with is almost always "what is waiting on me" or "what is stuck in
    /// talent_match", not "show me all of it".
    /// </remarks>
    [HttpGet]
    [Authorize(Policy = AuthorizationPolicies.RequireAdmin)]
    public async Task<IActionResult> ListWorkflows(
        [FromQuery] string? stage,
        [FromQuery] string? status,
        [FromQuery] int? projectId,
        [FromQuery] int page = 1,
        [FromQuery] int page_size = 25,
        CancellationToken cancellationToken = default)
    {
        // Validated here rather than passed through, so a bad stage is a clear
        // 400 instead of a query that silently matches nothing.
        string? normalisedStage = null;
        if (!string.IsNullOrWhiteSpace(stage))
        {
            try
            {
                normalisedStage = WorkflowStages.Normalise(stage);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        WorkflowRunStatus? normalisedStatus = null;
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!Enum.TryParse<WorkflowRunStatus>(status, ignoreCase: true, out var parsed))
                return BadRequest(new { error = $"Unknown workflow status '{status}'." });
            normalisedStatus = parsed;
        }

        var query = _db.WorkflowRuns.AsNoTracking().AsQueryable();

        if (normalisedStage is not null) query = query.Where(x => x.Stage == normalisedStage);
        if (normalisedStatus is not null) query = query.Where(x => x.Status == normalisedStatus);
        if (projectId is not null) query = query.Where(x => x.ProjectId == projectId);

        var count = await query.CountAsync(cancellationToken);
        var size = Math.Clamp(page_size, 1, 100);
        var pageNumber = Math.Max(page, 1);

        var rows = await query
            .OrderByDescending(x => x.CreatedAt)
            .Skip((pageNumber - 1) * size)
            .Take(size)
            .Select(x => new
            {
                x.Id,
                x.Objective,
                x.Stage,
                // The name, not the int. An admin reads this column, and a bare
                // number is not a status.
                Status = x.Status.ToString(),
                x.ProjectId,
                x.RequestedByUserId,
                x.ApprovalDecision,
                x.ApprovalRationale,
                x.ApprovedAt,
                x.CreatedAt,
                x.UpdatedAt,
            })
            .ToListAsync(cancellationToken);

        var result = new WebOfWork.Api.Helpers.PagedListResponse<object>
        {
            Count = count,
            Page = pageNumber,
            PageSize = size,
            Results = rows.Cast<object>().ToList(),
        };
        WebOfWork.Api.Helpers.PaginationLinks.Apply(
            Request, pageNumber, size, count,
            v => result.Next = v, v => result.Previous = v);

        return Ok(result);
    }

    /// <summary>
    /// The client gate. Approves a shortlist and invites the named freelancers.
    /// </summary>
    /// <remarks>
    /// Client-scoped, unlike <c>approve</c> which is admin-scoped. The two are
    /// deliberately separate endpoints rather than one endpoint that branches on
    /// stage: the request shapes have nothing in common (a list of freelancer ids
    /// versus a contract spec), and a shared route would need a nullable union
    /// body that accepts nonsense in both directions.
    /// </remarks>
    [HttpPost("{workflowRunId:int}/approve-shortlist")]
    [Authorize(Policy = AuthorizationPolicies.RequireClient)]
    public async Task<IActionResult> ApproveShortlist(
        int workflowRunId,
        [FromBody] ApproveShortlistRequest request,
        CancellationToken cancellationToken)
    {
        var userId = _currentUser.Id ?? throw new UnauthorizedAccessException("Authenticated client user is required.");

        var result = await _aiWorkflowService.ApproveShortlistAsync(
            workflowRunId, userId, request.FreelancerIds, request.Rationale,
            request.PackageChoices, cancellationToken);

        return Ok(new
        {
            run = result.Run,
            invitation_ids = result.InvitationIds,
            // Returned rather than thrown: a shortlist of ten where two have
            // already proposed should still reach the other eight, and the client
            // needs to show the client which two did not get contacted and why.
            skipped = result.Skipped.Select(s => new { freelancer_id = s.FreelancerId, reason = s.Reason }),
            // Which invitations were priced at a package the client did not pick.
            // The client sets the price, so a price they did not choose has to be
            // visible rather than assumed.
            fallback_package_ids = result.FallbackPackageIds,
        });
    }

    /// <summary>
    /// Resumes a graph paused at an approval gate.
    /// </summary>
    /// <remarks>
    /// <c>resume_value</c> is optional. The decision recorded at approval time is
    /// authoritative and is used when present; the argument exists for a run paused
    /// by a path that had nothing to record.
    /// </remarks>
    [HttpPost("{workflowRunId:int}/resume")]
    [Authorize(Policy = AuthorizationPolicies.RequireClient)]
    public async Task<IActionResult> ResumeWorkflow(
        int workflowRunId,
        [FromBody] ResumeWorkflowRequest? request,
        CancellationToken cancellationToken)
    {
        var userId = _currentUser.Id ?? throw new UnauthorizedAccessException("Authenticated client user is required.");
        var run = await _aiWorkflowService.ResumeWorkflowAsync(
            workflowRunId, userId, request?.ResumeValue, cancellationToken);
        return Ok(run);
    }

    [HttpPost("{workflowRunId:int}/cancel")]
    public async Task<IActionResult> CancelWorkflow(int workflowRunId, CancellationToken cancellationToken)
    {
        var userId = _currentUser.Id ?? throw new UnauthorizedAccessException("Authenticated user is required.");
        var run = await _aiWorkflowService.CancelWorkflowAsync(workflowRunId, userId, cancellationToken);
        return Ok(run);
    }
}

/// <summary>A client's shortlist decision.</summary>
/// <param name="FreelancerIds">
/// The freelancers the client chose, as `FreelancerProfile` ids. Duplicates are
/// collapsed by the service, and anyone who cannot be invited is reported back
/// rather than failing the whole approval.
/// </param>
/// <param name="PackageChoices">
/// Optional: which of each freelancer's packages the invitation is for, keyed by
/// `FreelancerProfile` id.
///
/// This exists because an invitation is priced by its package -- there is no
/// price on an invitation of its own. Without one the request carries
/// `PackageId = Guid.Empty`, `CreateInvitationAsync` rejects it with
/// "Invalid package ID", and every approved freelancer lands in `skipped`,
/// leaving a `Failed` run and zero invitations. That is what happened until this
/// field was added; the shortlist response has always carried each candidate's
/// packages, so the client had the information and simply had nowhere to put the
/// choice.
///
/// When a freelancer has no entry here the service falls back to their
/// lowest-priced package and reports that it did, rather than guessing
/// silently. The price is the client's decision, so the decision is recorded.
/// </param>
public sealed class ApproveShortlistRequest
{
    public List<Guid> FreelancerIds { get; set; } = new();
    public string? Rationale { get; set; }
    public Dictionary<Guid, Guid> PackageChoices { get; set; } = new();
}

public sealed class ResumeWorkflowRequest
{
    public string? ResumeValue { get; set; }
}

public sealed class StartWorkflowRequest
{    public string Objective { get; set; } = string.Empty;

    /// <summary>
    /// Which agent stage to run. Required.
    /// </summary>
    /// <remarks>
    /// Previously absent, and the agent service was called without a stage, so it
    /// had no way to choose a graph. The service validates this against the shared
    /// vocabulary and rejects an unknown value rather than defaulting -- a silent
    /// default would run the wrong agent and report success.
    /// </remarks>
    public string Stage { get; set; } = string.Empty;

    public string? Plan { get; set; }
    public int? ProjectId { get; set; }
}

public sealed class ApproveWorkflowRequest
{
    public string? Decision { get; set; }
    public string? Rationale { get; set; }
    public Guid? FreelancerId { get; set; }
    public decimal? TotalAmount { get; set; }
    public Guid? PackageId { get; set; }
    public int? ProjectId { get; set; }
}
