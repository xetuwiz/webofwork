using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using WebOfWork.Api.Authorization;
using WebOfWork.Api.Data;
using WebOfWork.Api.Models;
using WebOfWork.Api.Models.Enums;
using WebOfWork.Api.Services;

namespace WebOfWork.Api.Controllers;

/// <summary>
/// How long a run may sit at <c>Pending</c> before it is assumed abandoned.
/// </summary>
/// <remarks>
/// <para>
/// A run is created as <c>Pending</c> and leaves that state when the agent
/// service calls back. Ten minutes is comfortably longer than a full
/// plan → match → validate pass, which measures around 17 seconds, while still
/// being short enough that a developer does not spend an afternoon wondering
/// why the button does nothing.
/// </para>
/// <para>
/// Generous on purpose. The cost of being wrong in one direction and the other
/// are not comparable: too short and a legitimate slow run gets cancelled out
/// from under itself and the client gets a duplicate shortlist; too long and
/// nobody is blocked for more than the wait. When in doubt, wait longer.
/// </para>
/// </remarks>
internal static class AbandonedRunPolicy
{
    internal static readonly TimeSpan MaxAge = TimeSpan.FromMinutes(10);
}

/// <summary>
/// "Find best talent" -- runs the matching pipeline over a posted project and
/// produces a shortlist for the client to approve.
/// </summary>
/// <remarks>
/// <para>
/// Client-scoped and project-scoped. Every action checks that the caller posted
/// the project, because a shortlist is an invitation to spend the client's money
/// and to expose the client's brief to a scoring pipeline. Without the ownership
/// check, any authenticated client could run matching over someone else's project
/// and read the resulting shortlist.
/// </para>
/// <para>
/// The shortlist is a suggestion until the client approves it. Nothing is sent to
/// anyone by this controller -- outreach only happens through
/// <c>approve-shortlist</c> or the internal action endpoint, both of which re-check
/// that the approval is recorded.
/// </para>
/// </remarks>
[ApiController]
[Route("api/ai/talent-match")]
[Authorize(Policy = AuthorizationPolicies.RequireClient)]
public class AiTalentMatchController : ControllerBase
{
    private readonly IAiWorkflowService _workflows;
    private readonly ICurrentUser _currentUser;
    private readonly WebOfWorkDbContext _db;
    private readonly IStageResolver _stages;

    public AiTalentMatchController(
        IAiWorkflowService workflows,
        ICurrentUser currentUser,
        WebOfWorkDbContext db,
        IStageResolver stages)
    {
        _workflows = workflows;
        _currentUser = currentUser;
        _db = db;
        _stages = stages;
    }

    /// <summary>
    /// Starts matching for a project the caller posted.
    /// </summary>
    /// <remarks>
    /// The stage is derived from the project's state rather than taken from the
    /// request. A client asking for "best talent" on a project that already has a
    /// validated shortlist should be told the pipeline has moved on, not silently
    /// re-run from the top -- which is what a client-supplied stage would allow.
    /// </remarks>
    [HttpPost("start/{projectId:int}")]
    public async Task<IActionResult> Start(int projectId, [FromBody] StartTalentMatchRequest? request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.Id
            ?? throw new UnauthorizedAccessException("An authenticated client user is required.");

        var project = await _db.Projects.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == projectId, cancellationToken);

        if (project is null) return NotFound(new { error = $"Project {projectId} was not found." });

        // `Project.ClientId` is a foreign key to `client_profiles`, while the
        // token carries the *user* id. The two are generated independently --
        // `AuthService` does `new ClientProfile { User = user }` and lets the
        // database assign a different Guid to each -- so comparing them directly
        // compares two unrelated identifiers, which are never equal.
        //
        // The consequence was that this endpoint answered 401 for every client,
        // always: "Find Best Talent" could not be used by anyone. It went
        // unnoticed because the only test of this ownership check mocked the
        // service to throw, so the comparison never actually ran.
        //
        // `ProjectService.GetClientIdAsync` is this same resolution, and every
        // other project endpoint goes through it.
        var clientProfileId = await _db.ClientProfiles
            .Where(c => c.UserId == userId)
            .Select(c => (Guid?)c.Id)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new UnauthorizedAccessException("An authenticated client user is required.");

        if (project.ClientId != clientProfileId)
            throw new UnauthorizedAccessException("You do not own this project.");

        // A client-supplied plan means "re-run with these weights", which is a
        // deliberate replacement rather than a second click. It is validated here
        // so a bad value is a clear 400 naming the offending field, instead of a
        // run that starts, fails inside the agent service, and surfaces as a
        // generic failure the client cannot act on.
        var suppliedPlan = request?.Plan;
        if (suppliedPlan is not null)
        {
            var problem = suppliedPlan.Validate();
            if (problem is not null)
                return BadRequest(new { error = problem });
        }

        // `Pending` is ambiguous, and reading it as "alive" makes the feature
        // unrecoverable.
        //
        // A run is inserted as `Pending` and only leaves that state when the agent
        // service reports back. If the service is killed mid-run, the container
        // dies, or the process crashes between the INSERT and the first callback,
        // nothing ever reports back -- and the row sits at `Pending` forever.
        // Because `Pending` is inside the de-duplication set above, every later
        // click returns that corpse instead of starting a new one, and the client
        // is told `already_running`.
        //
        // Observed four times on project 11: runs 23 and 24 and two draft runs all
        // needed a manual `UPDATE workflow_runs SET status = 7` before the button
        // did anything. The write-side fixes stop new runs dying silently, but
        // nothing distinguishes a run that is thirty seconds old from one that died
        // this morning, so the ambiguity is resolved here as well.
        //
        // `PendingClientApproval` is deliberately NOT subject to this. It is a
        // resting place -- the run finished and is waiting for a person -- so it
        // must never be treated as abandoned, however old it is.
        var inFlight = await _db.WorkflowRuns
            .Where(x => x.ProjectId == projectId
                     && x.Stage == WorkflowStages.TalentMatch
                     && (x.Status == WorkflowRunStatus.Pending
                      || x.Status == WorkflowRunStatus.Running
                      || x.Status == WorkflowRunStatus.PendingClientApproval))
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

        var cutoff = DateTime.UtcNow - AbandonedRunPolicy.MaxAge;

        // Cancel the abandoned ones rather than merely skipping them.
        //
        // Filtering them out of the de-duplication set is enough to make the
        // button work again, but it leaves the row sitting at `Pending` forever
        // -- and `GetProjectWorkflowStatus` reads the newest run for a project, so
        // an abandoned row that happens to be the newest is exactly what makes a
        // page say "pending" over a run that has since been superseded. Cancelling
        // takes it out of every query that cares.
        var abandoned = inFlight
            .Where(x => x.Status == WorkflowRunStatus.Pending && x.UpdatedAt <= cutoff)
            .ToList();

        foreach (var stale in abandoned)
        {
            stale.Status = WorkflowRunStatus.Cancelled;
            stale.UpdatedAt = DateTime.UtcNow;
        }

        var existing = inFlight
            .Where(x => !abandoned.Contains(x))
            .FirstOrDefault();

        if (existing is not null && suppliedPlan is null)
        {
            // A run already in flight for this project means a second click, not a
            // new request. Returning the existing one is friendlier than starting a
            // duplicate pipeline that competes with it for the same shortlist rows.
            return Ok(new
            {
                workflow_run_id = existing.Id,
                stage = existing.Stage,
                status = existing.Status.ToString(),
                already_running = true,
            });
        }

        // Superseding an in-flight run, because the client has asked for a
        // different answer. The old run is marked cancelled rather than deleted:
        // its recommendations stay on record, so the shortlist that was shown
        // before is still auditable afterwards.
        //
        // Only done when a plan was supplied. Without one this branch is
        // unreachable, and cancelling a run the client is currently reading
        // because they pressed the button twice would be destructive.
        var superseded = new List<int>();
        if (suppliedPlan is not null)
        {
            foreach (var stale in inFlight)
            {
                stale.Status = WorkflowRunStatus.Cancelled;
                stale.UpdatedAt = DateTime.UtcNow;
                superseded.Add(stale.Id);
            }
        }

        var resolution = await _stages.ResolveForProjectAsync(projectId, cancellationToken);

        // The planner runs first inside the talent_match graph, so the stage is
        // talent_match even when the resolver says the project is further along.
        // If the project has genuinely moved past matching, say so rather than
        // starting a pipeline whose output the client will be told to ignore.
        if (resolution.Stage != WorkflowStages.TalentMatch && resolution.HasProposals)
        {
            return Conflict(new
            {
                error = "This project is already past talent matching.",
                resolved_stage = resolution.Stage,
                rationale = resolution.Rationale,
            });
        }

        var run = await _workflows.StartWorkflowAsync(
            userId,
            objective: $"Find the best-matched freelancers for project #{projectId} ({project.Title})",
            stage: WorkflowStages.TalentMatch,
            plan: suppliedPlan?.ToJson(),
            projectId: projectId,
            context: new
            {
                project_id = projectId,
                category_id = project.CategoryId,
                title = project.Title,
                budget = project.PricingType == ProjectPricingType.Fixed
                    ? (object?)project.FixedPrice
                    : new { min = project.MinPrice, max = project.MaxPrice },
                expected_days = project.ExpectedDays,
                // Repeated alongside `plan` because the agent service reads both:
                // `plan` is the authoritative source, and `context` is what the
                // dispatch contract documents. Sending it in one place only would
                // work until one of them changed.
                // As a JSON object, not `ToJson()`'s text. The planner reads
                // `context.plan` as a dict; quoted, it found a string, ignored the
                // client's weights entirely and used its own defaults.
                plan = suppliedPlan?.ToJsonValue(),
            },
            ct: cancellationToken);

        return Accepted(new
        {
            workflow_run_id = run.Id,
            stage = run.Stage,
            status = run.Status.ToString(),
            already_running = false,
            // Non-empty only on a re-run. The client uses it to say "this
            // replaced your previous shortlist" rather than quietly swapping the
            // list out from under someone who may have been reading it.
            superseded_workflow_run_ids = superseded,
        });
    }

    /// <summary>
    /// The shortlist produced so far, with the factor breakdown.
    /// </summary>
    /// <remarks>
    /// Ownership is re-checked here, not just at start. The run id is guessable --
    /// they are sequential integers -- so an unscoped read here would leak one
    /// client's freelancer shortlist to another.
    /// </remarks>
    [HttpGet("{workflowRunId:int}/shortlist")]
    public async Task<IActionResult> Shortlist(int workflowRunId, CancellationToken cancellationToken)
    {
        var userId = _currentUser.Id
            ?? throw new UnauthorizedAccessException("An authenticated client user is required.");

        var run = await _db.WorkflowRuns.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == workflowRunId, cancellationToken);

        if (run is null) return NotFound();

        if (run.RequestedByUserId != userId)
            throw new UnauthorizedAccessException("You do not own this workflow.");

        // The freelancer's identity travels with the recommendation, because a
        // shortlist a client cannot act on is not a shortlist.
        //
        // `AgentRecommendation.FreelancerId` is the **freelancer_profiles** id,
        // not the user id. Joining `users` on it directly matches zero rows -- the
        // name comes out null with no error anywhere, which is exactly how the
        // panel came to render four candidates all called "Freelancer".
        var recommendations = await (
                from r in _db.AgentRecommendations.AsNoTracking()
                join profile in _db.FreelancerProfiles.AsNoTracking()
                    on r.FreelancerId equals profile.Id
                join user in _db.Users.AsNoTracking()
                    on profile.UserId equals user.Id
                where r.WorkflowRunId == workflowRunId
                orderby r.Score descending
                select new { Recommendation = r, profile, user })
            .ToListAsync(cancellationToken);

        var (plan, planSource) = ResolveShortlistPlan(run.Plan, run.FinalOutput);

        return Ok(new
        {
            workflow_run_id = run.Id,
            stage = run.Stage,
            status = run.Status.ToString(),
            // The scoring weights this shortlist was produced under, so the client
            // can show them and offer a re-run with different ones.
            //
            // Parsed to a JSON string rather than an object because the plan is
            // jsonb stored as text, and forwarding it raw would hand the browser a
            // string it has to parse. A plan that will not parse comes back null:
            // the weights are explanatory, and failing the whole shortlist over
            // them would hide the answer the client actually came for.
            plan,
            // Whether these weights are ones the planner chose or ones a person
            // set. Without it the panel has to guess, and it was guessing "nobody
            // chose these" for every first run -- which is how a page full of real
            // planner weights ended up captioned "The AI has not chosen its
            // weights yet".
            plan_source = planSource,
            // Whether the approve action will do anything yet. Sending the button
            // before the shortlist exists produces a confusing "not awaiting
            // approval" error rather than a disabled control.
            awaiting_approval = run.Status == WorkflowRunStatus.PendingClientApproval,
            // Why there is nothing to approve, when there is nothing.
            //
            // The client renders a "No matches found" panel from this key and it was
            // never emitted, so an empty shortlist showed an empty list and no
            // explanation -- the reader had to work out from the absence of rows
            // whether the AI had found nobody or had simply not finished.
            no_match_reason = recommendations.Count == 0
                ? DescribeEmptyShortlist(run, recommendations.Count)
                : null,
            shortlist = recommendations.Select(x => new
            {
                x.Recommendation.Id,
                x.Recommendation.FreelancerId,
                // Who this is. Without it the client has four identical rows
                // labelled "Freelancer" and no way to open a profile, so the
                // approval step is a decision made blind.
                //
                // An inner join, so a recommendation whose profile or user has
                // been deleted drops out rather than coming back as a nameless
                // row. Such a candidate cannot be invited anyway.
                freelancer_name = x.user.FullName,
                tagline = x.profile.Tagline,
                average_rating = x.profile.AverageRating,
                review_count = x.profile.ReviewCount,
                experience_level = x.profile.ExperienceLevel,
                x.Recommendation.Score,
                x.Recommendation.Reason,
                x.Recommendation.FactorsJson,
                x.Recommendation.PortfolioSamplesJson,
                x.Recommendation.CreatedAt,
                // Parsed, not left as a JSON string. The client shows these two
                // lists as the evidence behind the score, and a screen cannot
                // read a JSON string without re-implementing this parser in
                // JavaScript. The raw blob stays for anything that needs it.
                strengths = Helpers.RecommendationFactors.ReadStrengths(x.Recommendation.FactorsJson),
                gaps = Helpers.RecommendationFactors.ReadGaps(x.Recommendation.FactorsJson),
            }),
        });
    }

    /// <summary>
    /// Resumes outreach after a shortlist approval.
    /// </summary>
    /// <remarks>
    /// Kept as an explicit endpoint rather than folded into
    /// <c>approve-shortlist</c>, because the approval and the resume are separable:
    /// the client may want to approve, let the invitations go, and see the
    /// outcome. Calling approve already resumes the graph; this exists for a client
    /// resuming after an interrupted run.
    /// </remarks>
    [HttpPost("resume/{workflowRunId:int}")]
    public async Task<IActionResult> Resume(int workflowRunId, CancellationToken cancellationToken)
    {
        var userId = _currentUser.Id
            ?? throw new UnauthorizedAccessException("An authenticated client user is required.");

        var run = await _workflows.ResumeWorkflowAsync(workflowRunId, userId, resumeValue: null, ct: cancellationToken);
        return Ok(new { workflow_run_id = run.Id, status = run.Status.ToString(), stage = run.Stage });
    }

    /// <summary>
    /// Reads a stored plan back as a JSON string, or null.
    /// </summary>
    /// <remarks>
    /// Returning a string keeps this honest about what is stored.
    /// <c>WorkflowRun.Plan</c> is jsonb held as text, and a run whose plan will
    /// not parse is a run the weights display should step aside for rather than
    /// fail the whole shortlist request over.
    /// </remarks>
    private static string? ParsePlan(string? plan)
    {
        if (string.IsNullOrWhiteSpace(plan)) return null;

        try
        {
            using var document = JsonDocument.Parse(plan);
            return document.RootElement.ValueKind == JsonValueKind.Object
                ? document.RootElement.GetRawText()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// The weights a shortlist was actually produced under, and who chose them.
    /// </summary>
    /// <param name="Plan">JSON text of the plan, or null if there is none to show.</param>
    /// <param name="Source">
    /// <c>"supplied"</c>, <c>"planner"</c>, or null when there is no plan at all.
    /// </param>
    /// <remarks>
    /// Two different columns hold these, and reading only the first is why the UI
    /// claimed the AI had not chosen its weights when it had.
    ///
    /// <list type="bullet">
    /// <item><c>WorkflowRun.Plan</c> is written only when the *client* supplied a
    /// plan, i.e. on a re-run from the weight sliders. On an ordinary first run it
    /// is null.</item>
    /// <item>The planner's own plan is written by <c>complete_workflow</c> to
    /// <c>WorkflowRun.FinalOutput</c>, under a <c>plan</c> key, alongside a
    /// <c>plan_source</c> of <c>"planner"</c>.</item>
    /// </list>
    ///
    /// Observed live: run 23's planning step succeeded and stored weights
    /// (<c>skill_coverage 0.4</c>, <c>portfolio_relevance 0.25</c>, ...) in the
    /// step log, and the shortlist page rendered the panel defaults while telling
    /// the client "The AI has not chosen its weights yet". The planner's weights
    /// were in the database the whole time, one column away.
    ///
    /// <see cref="WorkflowRun.Plan"/> wins when both are present, because a
    /// supplied plan is the one the client actually reviewed, and it is also the
    /// one the planner was *not* allowed to overwrite on a re-run.
    /// </remarks>
    /// <summary>
    /// A sentence explaining an empty shortlist, or null while it is still filling.
    /// </summary>
    /// <remarks>
    /// An empty list is ambiguous on its own: it reads the same whether the AI found
    /// nobody suitable or has not finished looking. The status tells the two apart,
    /// so this turns that into something a person can act on.
    /// </remarks>
    internal static string? DescribeEmptyShortlist(WorkflowRun run, int recommendationCount)
    {
        if (recommendationCount > 0)
            return null;

        return run.Status switch
        {
            WorkflowRunStatus.Pending or WorkflowRunStatus.Running =>
                // Not a finding. Saying "no matches" here would be a claim nobody has
                // made yet, and it would flicker away on the next poll.
                null,
            WorkflowRunStatus.Failed =>
                "This run failed before it produced a shortlist. Start it again to retry.",
            WorkflowRunStatus.PendingApproval =>
                "The shortlist did not pass its checks, so there is nothing to approve. "
                + "Adjust the weights and run it again.",
            WorkflowRunStatus.PendingManagerApproval =>
                "This run is waiting on a manager review before a shortlist is produced.",
            WorkflowRunStatus.Cancelled =>
                "This run was cancelled before it produced a shortlist.",
            _ =>
                "The AI scored the pool and nobody scored high enough to be worth "
                + "contacting. Loosen the weights or the hard filters and run it again.",
        };
    }

    internal static (string? Plan, string? Source) ResolveShortlistPlan(
        string? suppliedPlan,
        string? finalOutput)
    {
        var supplied = ParsePlan(suppliedPlan);
        if (supplied is not null) return (supplied, "supplied");

        var fromPlanner = ReadPlannerPlan(finalOutput);
        return fromPlanner ?? (null, null);
    }

    /// <summary>
    /// Pulls <c>plan</c> and <c>plan_source</c> out of a completed run's output.
    /// </summary>
    /// <remarks>
    /// Returns null -- not a partially built pair -- when the output is absent,
    /// unparseable, or shaped differently than expected. The caller cannot act on
    /// half a pair, and a weights panel is not worth failing a shortlist over.
    /// </remarks>
    private static (string? Plan, string? Source)? ReadPlannerPlan(string? finalOutput)
    {
        if (string.IsNullOrWhiteSpace(finalOutput)) return null;

        try
        {
            using var document = JsonDocument.Parse(finalOutput);
            if (document.RootElement.ValueKind != JsonValueKind.Object) return null;
            if (!document.RootElement.TryGetProperty("plan", out var plan)) return null;

            var planText = plan.ValueKind == JsonValueKind.Object
                ? plan.GetRawText()
                : null;
            if (planText is null) return null;

            // Absent rather than assumed "planner": the only writer of this blob
            // sets it, and defaulting would let a future writer go unlabelled.
            var source = document.RootElement.TryGetProperty("plan_source", out var s)
                && s.ValueKind == JsonValueKind.String
                ? s.GetString()
                : null;

            return (planText, source);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

/// <summary>
/// The body of a talent-match start, when there is one.
/// </summary>
/// <remarks>
/// Optional. Absent means "plan it yourself", which is the ordinary first run.
/// Present means the client has chosen the scoring weights and wants them used,
/// which is what makes the adjust-and-re-run control real rather than decorative.
/// </remarks>
public sealed class StartTalentMatchRequest
{
    /// <summary>
    /// The scoring weights and shortlist size, or null to let the planner decide.
    /// </summary>
    public MatchPlanDto? Plan { get; set; }
}

/// <summary>
/// A client's chosen scoring weights.
/// </summary>
/// <remarks>
/// The same five factors the planner produces, so a client adjusting a plan is
/// adjusting the planner's plan rather than a parallel set of numbers the
/// matcher has never heard of.
/// </remarks>
public sealed class MatchPlanDto
{
    public double SkillCoverage { get; set; } = 0.35;
    public double PortfolioRelevance { get; set; } = 0.20;
    public double PastSuccessRate { get; set; } = 0.20;
    public double AvailabilityAndSpeed { get; set; } = 0.15;
    public double BudgetFit { get; set; } = 0.10;

    /// <summary>How many freelancers to shortlist. The planner's own range is 5-15.</summary>
    public int ShortlistSize { get; set; } = 10;

    /// <summary>
    /// Returns a message naming the first problem, or null when the plan is sound.
    /// </summary>
    /// <remarks>
    /// Validated on the way in rather than in the agent service, so the client
    /// gets a 400 that names the field it sent wrong. The agent service validates
    /// the same rules independently -- it is reachable by other callers, and a
    /// second opinion that agrees is worth having -- but the round trip is not
    /// where a user should discover they typed -1 into a weight box.
    /// </remarks>
    public string? Validate()
    {
        var weights = new (string Name, double Value)[]
        {
            ("skill_coverage", SkillCoverage),
            ("portfolio_relevance", PortfolioRelevance),
            ("past_success_rate", PastSuccessRate),
            ("availability_and_speed", AvailabilityAndSpeed),
            ("budget_fit", BudgetFit),
        };

        foreach (var (name, value) in weights)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
                return $"weight '{name}' is not a number.";

            if (value < 0 || value > 1)
                return $"weight '{name}' is {value}. Every weight must be between 0 and 1.";
        }

        var total = weights.Sum(w => w.Value);
        // The weights are relative: a total of 1.3 over-scores every candidate
        // uniformly, which looks like a stronger shortlist rather than a bug.
        if (Math.Abs(total - 1.0) > 0.001)
            return $"weights sum to {total:0.###}, not 1. They are relative, so the total "
                 + "is what decides the scale of every score.";

        if (ShortlistSize < 5 || ShortlistSize > 15)
            return $"shortlist_size is {ShortlistSize}. It must be between 5 and 15.";

        return null;
    }

    /// <summary>
    /// The plan in the shape the agent service expects.
    /// </summary>
    /// <remarks>
    /// snake_case keys, because this crosses to a Python service whose Pydantic
    /// models are snake_case by convention and whose normalisation layer maps
    /// .NET camelCase to it. Emitting camelCase here would produce a plan whose
    /// weights silently failed to bind, and the run would fall back to defaults --
    /// the precise silent-wrong-numbers failure this DTO exists to prevent.
    /// </remarks>
    public string ToJson() => JsonSerializer.Serialize(new
    {
        weights = new
        {
            skill_coverage = SkillCoverage,
            portfolio_relevance = PortfolioRelevance,
            past_success_rate = PastSuccessRate,
            availability_and_speed = AvailabilityAndSpeed,
            budget_fit = BudgetFit,
        },
        // Omitted rather than sent as null: the agent service's own defaults for
        // these are sensible, and an explicit null would be a value it has to
        // decide how to interpret.
        shortlist_size = ShortlistSize,
    });

    /// <summary>
    /// The same plan, as a JSON value rather than as text.
    /// </summary>
    /// <remarks>
    /// Two shapes of one thing, deliberately. <see cref="ToJson"/> is what goes
    /// into the jsonb <c>Plan</c> column, which is text by mapping. This one goes
    /// into the outgoing agent payload, where it has to arrive as an object.
    /// </remarks>
    /// <remarks>
    /// Sending <c>ToJson()</c> there shipped the plan as a quoted string. The
    /// planner's input handler guards with <c>isinstance(dict)</c>, found a
    /// string, and fell back to its own default weights -- so "Re-run with these
    /// weights" was silently inert: the run completed, produced a shortlist, and
    /// used weights the client had explicitly overridden.
    /// </remarks>
    public JsonElement ToJsonValue()
    {
        using var document = JsonDocument.Parse(ToJson());
        return document.RootElement.Clone();
    }
}
