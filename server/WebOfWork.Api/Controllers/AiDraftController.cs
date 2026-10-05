using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebOfWork.Api.Authorization;
using WebOfWork.Api.Data;
using WebOfWork.Api.Models;
using WebOfWork.Api.Models.Enums;
using WebOfWork.Api.Services;

namespace WebOfWork.Api.Controllers;

/// <summary>
/// "Help me draft" -- turns a client's free-text brief into a structured draft.
/// </summary>
/// <remarks>
/// <para>
/// This is a thin client for the draft agent stage. It deliberately does not
/// create a project, and deliberately returns a draft the client is free to edit,
/// ignore, or discard.
/// </para>
/// <para>
/// That matters because the agent's output contains database ids. The most likely
/// failure of a model asked to fill in a category is to invent one, so the
/// grounding checks in the agent are what make the output usable at all -- and a
/// draft that silently became a posted project would publish a hallucinated
/// category id to the freelancer-facing browse page. The client submits through
/// the existing <c>POST /api/projects/create</c>, which re-validates.
/// </para>
/// </remarks>
[ApiController]
[Route("api/ai/draft")]
[Authorize(Policy = AuthorizationPolicies.RequireClient)]
public class AiDraftController : ControllerBase
{
    private readonly IAiWorkflowService _workflows;
    private readonly ICurrentUser _currentUser;
    private readonly WebOfWorkDbContext _db;

    // No `IStageResolver` here, deliberately. The draft stage is always entered
    // from free text with no pipeline history, so there is nothing to resolve --
    // unlike talent matching, which depends on how far the project has already
    // progressed. Injecting it would suggest the draft stage can be resumed
    // somewhere in a pipeline, which it cannot.
    public AiDraftController(
        IAiWorkflowService workflows,
        ICurrentUser currentUser,
        WebOfWorkDbContext db)
    {
        _workflows = workflows;
        _currentUser = currentUser;
        _db = db;
    }

    /// <summary>
    /// Produces a draft from whatever the client has typed so far.
    /// </summary>
    /// <remarks>
    /// Returns the run, not the draft alone. The agent is asynchronous relative to
    /// the client's form -- the client has to keep typing, and a synchronous call
    /// that blocked on a model would either stall the form or time out. The run id
    /// is how the client polls for the result.
    /// </remarks>
    [HttpPost("draft")]
    public async Task<IActionResult> Draft(
        [FromBody] DraftProjectRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.PartialText))
            return BadRequest(new { error = "partial_text is required." });

        // A length cap, because this text is embedded in the agent prompt. Without
        // one, a pasted document produces a prompt the model provider rejects and
        // the failure surfaces as an opaque 500 from the agent service.
        if (request.PartialText.Length > MaxPartialTextLength)
            return BadRequest(new
            {
                error = $"partial_text must be at most {MaxPartialTextLength} characters; {request.PartialText.Length} supplied.",
            });

        var userId = _currentUser.Id
            ?? throw new UnauthorizedAccessException("An authenticated client user is required.");

        // Category and skill ids are resolved before the agent runs rather than
        // asked for by name in the prompt. The agent needs real ids for its
        // grounding checks, and giving it the valid set up front is what stops it
        // inventing one.
        //
        // Categories only. The skills are deliberately NOT nested here.
        //
        // They were, and that cost two things. The payload carried all 654 skills
        // on every draft, which is what pushed the conversation past Groq's
        // request limit twice ("413 Payload Too Large") and forced the chain onto
        // a fallback provider mid-draft. And a 654-row flat list is worse for
        // accuracy, not better: a live draft asked for a responsive website and
        // came back with ClickHouse, Snowflake and BigQuery, because the model
        // was choosing from everything at once instead of from the ~200 skills
        // that actually apply.
        //
        // The agent has `list_skills_by_category` for the narrow lookup, and it
        // used it -- both times. One category's skills is the right size to
        // choose from: a list the model can actually hold in mind.
        var categories = await _db.Categories.AsNoTracking()
            .OrderBy(c => c.Name)
            .Select(c => new { c.Id, c.Name })
            .ToListAsync(cancellationToken);

        var run = await _workflows.StartWorkflowAsync(
            userId,
            objective: string.IsNullOrWhiteSpace(request.TitleHint)
                ? $"Draft a project brief from the client's notes: {Truncate(request.PartialText, 200)}"
                : request.TitleHint.Trim(),
            stage: WorkflowStages.Draft,
            plan: null,
            projectId: null,
            context: new
            {
                partial_text = request.PartialText,
                title_hint = request.TitleHint,
                category_hint = request.CategoryHint,
                budget_hint_usd = request.BudgetHintUsd,
                delivery_days_hint = request.DeliveryDaysHint,
                // The closed vocabulary, so the agent can ground its category and
                // skill choices instead of guessing.
                categories,
            },
            ct: cancellationToken);

        return Accepted(new
        {
            workflow_run_id = run.Id,
            stage = run.Stage,
            status = run.Status.ToString(),
            // Null until the agent writes back through the internal callback.
            draft = run.FinalOutput is null ? null : new { raw = run.FinalOutput },
        });
    }

    /// <summary>
    /// Polls for a draft.
    /// </summary>
    /// <remarks>
    /// Scoped to the requesting user, so one client cannot read another's draft by
    /// guessing a run id.
    /// </remarks>
    [HttpGet("{workflowRunId:int}")]
    public async Task<IActionResult> GetDraft(int workflowRunId, CancellationToken cancellationToken)
    {
        var userId = _currentUser.Id
            ?? throw new UnauthorizedAccessException("An authenticated client user is required.");

        var run = await _db.WorkflowRuns.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == workflowRunId && x.RequestedByUserId == userId, cancellationToken);

        if (run is null) return NotFound();

        return Ok(new
        {
            workflow_run_id = run.Id,
            stage = run.Stage,
            status = run.Status.ToString(),
            completed = run.Status is WorkflowRunStatus.Completed or WorkflowRunStatus.Failed,
            draft = run.FinalOutput is null ? null : new { raw = run.FinalOutput },
        });
    }

    /// <summary>
    /// 8k characters.
    /// </summary>
    /// <remarks>
    /// Roughly 2k tokens. Long enough for a detailed brief, short enough that the
    /// whole prompt stays inside a typical provider's context window with the
    /// category list and the system prompt around it.
    /// </remarks>
    public const int MaxPartialTextLength = 8000;

    private static string Truncate(string value, int max)
        => value.Length <= max ? value : value[..max] + "…";
}

/// <param name="PartialText">Whatever the client has typed. The only required field.</param>
public sealed class DraftProjectRequest
{
    public string PartialText { get; set; } = string.Empty;

    /// <summary>Optional steering. The agent treats these as hints, not answers.</summary>
    public string? TitleHint { get; set; }
    public string? CategoryHint { get; set; }
    public decimal? BudgetHintUsd { get; set; }
    public int? DeliveryDaysHint { get; set; }
}
