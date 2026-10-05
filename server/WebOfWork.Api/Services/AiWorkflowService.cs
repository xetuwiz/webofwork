using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Text.Json;
using System.Net.Http.Json;
using WebOfWork.Api.Configuration;
using WebOfWork.Api.Data;
using WebOfWork.Api.DTOs.Invitations;
using WebOfWork.Api.Models;
using WebOfWork.Api.Models.Enums;

namespace WebOfWork.Api.Services;

public interface IAiWorkflowService
{
    Task<WorkflowRun> StartWorkflowAsync(Guid requestedByUserId, string objective, string stage, string? plan = null, int? projectId = null, object? context = null, CancellationToken ct = default, bool dispatchToAgent = true);

    /// <summary>
    /// Creates a run without calling the agent service.
    /// </summary>
    /// <remarks>
    /// <para>
    /// For callers on a user-facing path -- a proposal arriving, a payment being
    /// verified -- where the workflow is a side effect and must not be able to
    /// delay or fail the operation that triggered it.
    /// </para>
    /// <para>
    /// The plain <c>StartWorkflowAsync</c> awaits an HTTP call to the Python
    /// service, which on a cold model can take seconds. Awaiting that from
    /// <c>CreateProposalAsync</c> would mean a freelancer's proposal submission
    /// waits on an LLM, and fails outright if the AI service is down. The run is
    /// already durable in the database, so it survives; only the agent call is
    /// deferred to <see cref="IAiWorkflowTrigger"/>, which does not block.
    /// </para>
    /// </remarks>
    Task<WorkflowRun> StartWorkflowDeferredAsync(Guid requestedByUserId, string objective, string stage, int? projectId = null, object? context = null, CancellationToken ct = default);

    /// <summary>
    /// Calls the agent service for an existing run. Safe to retry: the run's
    /// status is the source of truth and the agents are idempotent per run.
    /// </summary>
    Task DispatchAsync(int workflowRunId, object? context = null, CancellationToken ct = default);

    /// <summary>
    /// The manager gate: approve a run whose stage waits on an admin, creating
    /// the contract described by the plan.
    /// </summary>
    Task<WorkflowRun> ApproveWorkflowAsync(int workflowRunId, Guid approvedByUserId, string? decision = null, string? rationale = null, WorkflowContractSpec? contractSpec = null, CancellationToken ct = default);

    /// <summary>
    /// The client gate: approve a shortlist, then run outreach.
    /// </summary>
    /// <remarks>
    /// Separate from <see cref="ApproveWorkflowAsync"/> because the two are
    /// different decisions with different consequences. Approving a shortlist
    /// commits the client to contacting people; approving a contract commits money.
    /// Overloading one method meant the contract-creating path ran for a
    /// shortlist approval too, and the two statuses collapsed into one value, so a
    /// client polling for "is this waiting on me?" could not be answered.
    /// </remarks>
    /// <param name="packageChoices">
    /// Which package each invitation is for, keyed by `FreelancerProfile` id.
    /// Optional: a freelancer absent from it is offered their lowest-priced
    /// package and the fallback is reported in <see cref="ShortlistApprovalResult.FallbackPackageIds"/>.
    /// </param>
    Task<ShortlistApprovalResult> ApproveShortlistAsync(
        int workflowRunId,
        Guid approverId,
        IReadOnlyCollection<Guid> approvedFreelancerIds,
        string? rationale = null,
        IReadOnlyDictionary<Guid, Guid>? packageChoices = null,
        CancellationToken ct = default);

    /// <summary>
    /// Replays a paused graph from the recorded human decision.
    /// </summary>
    Task<WorkflowRun> ResumeWorkflowAsync(int workflowRunId, Guid userId, string? resumeValue = null, CancellationToken ct = default);

    Task<WorkflowRun> CancelWorkflowAsync(int workflowRunId, Guid requestedByUserId, CancellationToken ct = default);
    Task<WorkflowStepLog> AppendStepLogAsync(int workflowRunId, string stepName, string? agentName, string? input, string? output, string status, long durationMs, CancellationToken ct = default);
    Task<ToolCallLog> AppendToolCallLogAsync(int workflowRunId, string toolName, string? input, string? output, bool success, long durationMs, CancellationToken ct = default);
    Task<AgentRecommendation> AppendAgentRecommendationAsync(int workflowRunId, AgentRecommendation recommendation, CancellationToken ct = default);
    Task<ProposalScreening> AppendProposalScreeningAsync(int workflowRunId, ProposalScreening screening, CancellationToken ct = default);
    Task<ValidationResult> AppendValidationResultAsync(int workflowRunId, ValidationResult result, CancellationToken ct = default);
    /// <param name="status">
    /// The status to land the run in. Null leaves it Completed.
    /// </param>
    Task<WorkflowRun> CompleteWorkflowAsync(int workflowRunId, string? output, string? status = null, CancellationToken ct = default);
}

/// <summary>What happened when a client approved a shortlist.</summary>
/// <param name="Run">The updated run.</param>
/// <param name="InvitationIds">Invitations actually created.</param>
/// <param name="Skipped">
/// Freelancers the client approved but who could not be invited, with the reason.
/// Reported rather than thrown: a shortlist of ten where two already proposed
/// should still reach the other eight.
/// </param>
public sealed record ShortlistApprovalResult(
    WorkflowRun Run,
    IReadOnlyList<int> InvitationIds,
    IReadOnlyList<(Guid FreelancerId, string Reason)> Skipped,
    /// <summary>
    /// Freelancers invited at a package the client did not choose.
    /// </summary>
    /// <remarks>
    /// Reported so the UI can say which price was assumed rather than silently
    /// presenting it as the client's own decision. The price is the client's call;
    /// a fallback they did not make is a fallback they should be able to see.
    /// </remarks>
    IReadOnlyList<Guid> FallbackPackageIds = null!);

public sealed record WorkflowContractSpec(
    Guid FreelancerId,
    decimal TotalAmount,
    Guid? PackageId = null,
    int? ProjectId = null);

public class AiWorkflowService : IAiWorkflowService
{
    /// <summary>
    /// Ceiling on a single shortlist approval.
    /// </summary>
    /// <remarks>
    /// Matches the planner agent's maximum shortlist size, so a shortlist the
    /// planner itself produced can always be approved. Set here rather than read
    /// from configuration because it is a safety bound on how many invitations one
    /// click can create, and a bound that can be raised by a config edit is not a
    /// bound.
    /// </remarks>
    public const int MaxShortlistApprovals = 15;

    /// <summary>
    /// Wire format for the recorded human decision.
    /// </summary>
    /// <remarks>
    /// camelCase, to match every other JSON payload this API emits. The value is
    /// stored as jsonb and read back by the Python agent service, so the property
    /// names are a contract with another process rather than an internal detail.
    /// </remarks>
    private static readonly JsonSerializerOptions ResumeJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    /// <summary>
    /// A jsonb column's text, as the JSON value it holds.
    /// </summary>
    /// <remarks>
    /// <para>
    /// These columns are jsonb stored as <see cref="string"/>, because EF's Npgsql
    /// mapping is configured that way. Embedding such a column in an outgoing
    /// payload by value therefore sends it as a JSON <em>string</em> -- the agent
    /// receives <c>"{\"freelancerIds\":[…]}"</c> where it expects an object.
    /// </para>
    /// <para>
    /// Python guards for this with <c>isinstance(dict)</c> and finds a string, so
    /// it reads an empty decision and the stage proceeds with nobody approved. The
    /// run still completes and still reports success. That is the failure this
    /// exists to prevent: approving a shortlist would send no invitations at all.
    /// </para>
    /// <para>
    /// Text that does not parse is passed through as the string it already was.
    /// The consumer then sees the same shape it saw before, rather than the value
    /// disappearing -- a hand-edited column degrades to the previous behaviour
    /// instead of to silence.
    /// </para>
    /// </remarks>
    private static object? AsJsonValue(string? jsonb)
    {
        if (string.IsNullOrWhiteSpace(jsonb))
            return null;

        try
        {
            using var document = JsonDocument.Parse(jsonb);
            return document.RootElement.Clone();
        }
        catch (JsonException)
        {
            return jsonb;
        }
    }

    private readonly WebOfWorkDbContext _db;
    private readonly IBackgroundJobClient _jobs;
    private readonly IHttpClientFactory? _httpClientFactory;
    private readonly AgenticAiOptions? _aiOptions;
    private readonly IInvitationService? _invitations;

    private static string? SerializeJsonbString(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        try
        {
            using var document = JsonDocument.Parse(value);
            return document.RootElement.GetRawText();
        }
        catch (JsonException)
        {
            return JsonSerializer.Serialize(value);
        }
    }

    public AiWorkflowService(
        WebOfWorkDbContext db,
        IBackgroundJobClient jobs,
        IHttpClientFactory? httpClientFactory = null,
        IOptions<AgenticAiOptions>? aiOptions = null,
        IInvitationService? invitations = null)
    {
        _db = db;
        _jobs = jobs;
        _httpClientFactory = httpClientFactory;
        _aiOptions = aiOptions?.Value;
        _invitations = invitations;
    }

    public async Task<WorkflowRun> StartWorkflowAsync(
        Guid requestedByUserId,
        string objective,
        string stage,
        string? plan = null,
        int? projectId = null,
        object? context = null,
        CancellationToken ct = default,
        // Defaults to true so every existing caller keeps its current behaviour.
        // Only `StartWorkflowDeferredAsync` passes false.
        bool dispatchToAgent = true)
    {
        if (string.IsNullOrWhiteSpace(objective))
            throw new ArgumentException("Workflow objective is required.", nameof(objective));

        // Normalised rather than trimmed, so an unknown stage is rejected here
        // instead of reaching the Python service and dispatching to a graph that
        // does not exist.
        var normalisedStage = WorkflowStages.Normalise(stage);

        var run = new WorkflowRun
        {
            Objective = objective.Trim(),
            Stage = normalisedStage,
            Plan = SerializeJsonbString(plan),
            ProjectId = projectId,
            RequestedByUserId = requestedByUserId,
            Status = WorkflowRunStatus.Pending,
            ApprovalDecision = "Pending",
            FinalOutput = null,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.WorkflowRuns.Add(run);
        await _db.SaveChangesAsync(ct);

        // Only a manager-gated stage pings admins. A shortlist waiting on its
        // client does not want an admin notification: the admin cannot act on it,
        // so the alert trains everyone to ignore workflow notifications.
        //
        // The `WorkflowApprovalRequired` type exists only for this, and it is one
        // of the five types in the catalogue that never fired.
        if (WorkflowStages.ApprovalGateFor(normalisedStage) == WorkflowRunStatus.PendingManagerApproval)
        {
            await NotifyAdminsForApprovalAsync(run, ct);
        }

        // Skipped when the caller is on a user-facing path and intends to
        // dispatch separately, so a slow or unreachable agent service cannot
        // delay or fail the operation that triggered the run.
        if (dispatchToAgent)
            await TryStartPythonAgentAsync(run, context, ct, plan);

        return run;
    }

    public async Task<WorkflowRun> StartWorkflowDeferredAsync(
        Guid requestedByUserId, string objective, string stage,
        int? projectId = null, object? context = null, CancellationToken ct = default)
    {
        // Created with the agent call skipped, then handed to the trigger, which
        // dispatches it without the caller's request waiting.
        return await StartWorkflowAsync(
            requestedByUserId, objective, stage, plan: null, projectId: projectId,
            context: null, ct: ct, dispatchToAgent: false);
    }

    public async Task DispatchAsync(int workflowRunId, object? context = null, CancellationToken ct = default)
    {
        var run = await _db.WorkflowRuns.FirstOrDefaultAsync(x => x.Id == workflowRunId, ct);
        if (run is null) return;

        // The run's own stored plan, not just the caller's context. A re-dispatch
        // after a transient failure has to reproduce the original decision --
        // re-running the planner over a client-chosen plan would silently replace
        // the weights they set with ones they did not.
        await TryStartPythonAgentAsync(run, context, ct, run.Plan);
    }

    private async Task NotifyAdminsForApprovalAsync(WorkflowRun run, CancellationToken ct)
    {
        var adminIds = await _db.Users.AsNoTracking()
            .Where(x => x.Role == UserRole.Admin && !x.IsDeleted)
            .Select(x => x.Id)
            .ToListAsync(ct);

        if (adminIds.Count == 0) return;

        foreach (var adminId in adminIds)
        {
            _jobs.Enqueue<WebOfWork.Api.Jobs.PublishNotificationJob>(job => job.ExecuteAsync(
                adminId,
                $"Workflow #{run.Id} is awaiting your approval.",
                NotificationType.WorkflowApprovalRequired,
                CancellationToken.None));
        }
    }

    /// <summary>
    /// The manager gate. Approves a run and creates the contract its plan describes.
    /// </summary>
    /// <remarks>
    /// Rejects unless the run is actually waiting on a manager, and refuses a
    /// shortlist approval outright. That refusal is the point of splitting the
    /// gates: this method creates a contract and enqueues a contract offer, and
    /// routing a client's "yes, contact these five people" through it would draft
    /// a contract with whoever happened to be first in the list.
    /// </remarks>
    public async Task<WorkflowRun> ApproveWorkflowAsync(int workflowRunId, Guid approvedByUserId, string? decision = null, string? rationale = null, WorkflowContractSpec? contractSpec = null, CancellationToken ct = default)
    {
        var run = await _db.WorkflowRuns
            .FirstOrDefaultAsync(x => x.Id == workflowRunId, ct)
            ?? throw new KeyNotFoundException($"Workflow run {workflowRunId} was not found.");

        var approvalDecision = string.IsNullOrWhiteSpace(decision) ? "Approved" : decision.Trim();
        var rejected = string.Equals(approvalDecision, "Rejected", StringComparison.OrdinalIgnoreCase);

        // A rejection is always allowed -- someone must be able to stop a run --
        // but an approval must not be routed through the wrong gate.
        //
        // Only `PendingClientApproval` is refused. A manager approving a run that
        // is waiting on its *client* would create a contract with whichever
        // freelancer the model happened to rank first, when what the client
        // actually said was "yes, contact these five people". That is the specific
        // confusion the split exists to prevent.
        //
        // Every other status is allowed through, including `Pending` and
        // `Running`. A run that never reached the agent service -- no AI
        // credentials, a timeout, a crashed process -- stays Pending forever, and
        // an admin clearing a stuck run by hand is a legitimate action. Refusing
        // non-approval statuses would leave those runs unrescuable.
        if (!rejected && run.Status == WorkflowRunStatus.PendingClientApproval)
            throw new InvalidOperationException(
                $"Workflow {workflowRunId} is waiting on the client to approve a shortlist, not on a manager. " +
                "Use the shortlist approval endpoint so invitations go to the people the client chose.");

        run.Status = rejected ? WorkflowRunStatus.Rejected : WorkflowRunStatus.Approved;
        run.ApprovalDecision = approvalDecision;
        run.ApprovalRationale = string.IsNullOrWhiteSpace(rationale) ? null : rationale.Trim();
        run.ApprovedByUserId = approvedByUserId;
        run.ApprovedAt = DateTime.UtcNow;
        run.UpdatedAt = DateTime.UtcNow;
        run.FinalOutput ??= rejected ? "Rejected by admin." : "Approved by admin and ready for execution.";

        if (!rejected)
        {
            contractSpec ??= TryReadContractSpec(run.Plan);
            if (contractSpec is not null)
            {
                var projectId = contractSpec.ProjectId ?? run.ProjectId
                    ?? throw new InvalidOperationException("A project is required to create the approved contract.");
                if (run.ProjectId.HasValue && run.ProjectId.Value != projectId)
                    throw new InvalidOperationException("The approval project does not match the workflow project.");
                if (contractSpec.TotalAmount <= 0)
                    throw new InvalidOperationException("The approved contract amount must be greater than zero.");

                if (await _db.Contracts.AnyAsync(x => x.ProjectId == projectId, ct))
                    throw new InvalidOperationException($"Project {projectId} already has a contract.");

                var project = await _db.Projects.FirstOrDefaultAsync(x => x.Id == projectId, ct)
                    ?? throw new KeyNotFoundException($"Project {projectId} was not found.");
                var freelancer = await _db.FreelancerProfiles.FirstOrDefaultAsync(x => x.Id == contractSpec.FreelancerId, ct)
                    ?? throw new KeyNotFoundException($"Freelancer {contractSpec.FreelancerId} was not found.");

                var contract = new Contract
                {
                    ProjectId = project.Id,
                    ClientId = project.ClientId,
                    FreelancerId = freelancer.Id,
                    PackageId = contractSpec.PackageId,
                    TotalAmount = contractSpec.TotalAmount,
                    ClientSignature = "AI-Workflow-Approval",
                    ClientSignatureType = "workflow",
                    Status = ContractStatus.Offered,
                    ClientSignedAt = DateTime.UtcNow
                };
                _db.Contracts.Add(contract);
                await _db.SaveChangesAsync(ct);

                _jobs.Enqueue<WebOfWork.Api.Jobs.ContractOfferNotificationJob>(job => job.ExecuteAsync(
                    freelancer.UserId, contract.Id, contract.TotalAmount, CancellationToken.None));
            }
        }

        await _db.SaveChangesAsync(ct);
        return run;
    }

    /// <summary>
    /// The client gate: record which freelancers the client approved, then invite
    /// exactly those people.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The approved ids are written to <c>ResumeValue</c> before any invitation is
    /// created. That ordering is deliberate: if the process dies halfway through
    /// the loop, the human's decision is already durable and the run can be
    /// resumed without asking them again. The other order would lose the decision
    /// along with the process.
    /// </para>
    /// <para>
    /// Invitations are created one at a time with individual failure capture,
    /// rather than as a batch. <c>CreateInvitationAsync</c> already refuses a
    /// freelancer who has proposed on the project, which is routine in a shortlist
    /// assembled from people who applied. Failing the whole approval for that
    /// would mean a client who approved ten people gets no invitations at all
    /// because one of them had already applied.
    /// </para>
    /// </remarks>
    public async Task<ShortlistApprovalResult> ApproveShortlistAsync(
        int workflowRunId,
        Guid approverId,
        IReadOnlyCollection<Guid> approvedFreelancerIds,
        string? rationale = null,
        IReadOnlyDictionary<Guid, Guid>? packageChoices = null,
        CancellationToken ct = default)
    {
        var run = await _db.WorkflowRuns
            .FirstOrDefaultAsync(x => x.Id == workflowRunId, ct)
            ?? throw new KeyNotFoundException($"Workflow run {workflowRunId} was not found.");

        if (run.Status != WorkflowRunStatus.PendingClientApproval)
            throw new InvalidOperationException(
                $"Workflow {workflowRunId} is not awaiting a shortlist approval (status {run.Status}).");

        if (approvedFreelancerIds.Count == 0)
            throw new ArgumentException(
                "Approving an empty shortlist would end the run without contacting anyone.",
                nameof(approvedFreelancerIds));

        // A cap, because each name becomes an invitation plus a notification. 15
        // matches the planner's maximum shortlist size, so a compliant shortlist
        // can never be rejected here.
        if (approvedFreelancerIds.Count > MaxShortlistApprovals)
            throw new ArgumentException(
                $"A shortlist approval may name at most {MaxShortlistApprovals} freelancers; {approvedFreelancerIds.Count} were supplied.",
                nameof(approvedFreelancerIds));

        var projectId = run.ProjectId
            ?? throw new InvalidOperationException($"Workflow {workflowRunId} has no project, so it cannot invite anyone.");

        var project = await _db.Projects
            .Include(x => x.Client)
            .FirstOrDefaultAsync(x => x.Id == projectId, ct)
            ?? throw new KeyNotFoundException($"Project {projectId} was not found.");

        // Only the client who posted the project may approve who gets contacted.
        // Without this, any signed-in user who guessed a run id could send
        // invitations out of someone else's account.
        //
        // Compared against `Client.UserId`, not `Project.ClientId`. The latter is
        // the `ClientProfile` primary key, so comparing it to a user id never
        // matches and every approval is rejected. Same distinction `DisputeService`
        // makes when it checks `contract.Client.UserId`.
        var clientUserId = project.Client?.UserId;
        if (clientUserId is null || clientUserId.Value != approverId)
            throw new UnauthorizedAccessException("Only the client who posted this project can approve its shortlist.");

        // Deduplicated before the ids are stored, so the recorded decision is the
        // one that will actually be acted on rather than a set that would produce
        // duplicate invitations.
        var distinct = approvedFreelancerIds.Distinct().ToList();

        var invitations = _invitations
            ?? throw new InvalidOperationException(
                "Invitations cannot be sent because no invitation service is registered. " +
                "The approval could not be recorded, so nothing has changed.");

        // Decision durable first, invitations second.
        //
        // Serialised camelCase explicitly. `JsonSerializer.Serialize` with no
        // options would emit `FreelancerIds`, and this jsonb is read back by the
        // Python agent service -- the rest of this API is camelCase on the wire,
        // and a resume value that disagreed with every other payload it sits
        // alongside would be a trap for the next reader.
        run.ResumeValue = SerializeJsonbString(JsonSerializer.Serialize(
            new ShortlistApproval
            {
                FreelancerIds = distinct,
                ApprovedBy = approverId,
                ApprovedAt = DateTime.UtcNow,
            },
            ResumeJsonOptions));
        run.Status = WorkflowRunStatus.Approved;
        run.ApprovalDecision = "Shortlist Approved";
        run.ApprovalRationale = string.IsNullOrWhiteSpace(rationale) ? null : rationale.Trim();
        run.ApprovedByUserId = approverId;
        run.ApprovedAt = DateTime.UtcNow;
        run.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        var invitationIds = new List<int>(distinct.Count);
        var skipped = new List<(Guid, string)>(distinct.Count);

        // Which package each invitation is priced from.
        //
        // An invitation carries no price of its own; the price is the package's.
        // Omitting it leaves `PackageId = Guid.Empty`, which
        // `CreateInvitationAsync` rejects outright -- so every approved
        // freelancer would land in `skipped` and the run would be marked Failed
        // with nothing sent. That is not a hypothetical: it is what this loop did,
        // on both the approval path and the internal action, until the package
        // was resolved here.
        var fallbacks = new List<Guid>();

        foreach (var freelancerId in distinct)
        {
            try
            {
                var packageId = packageChoices is not null
                    && packageChoices.TryGetValue(freelancerId, out var chosen) && chosen != Guid.Empty
                        ? chosen
                        : await ResolveDefaultPackageIdAsync(freelancerId, ct);

                if (packageId is null)
                {
                    skipped.Add((freelancerId,
                        "This freelancer has no package set up, so there is no price to invite them at. "
                        + "They need to add a package to their profile first."));
                    continue;
                }

                if (packageChoices is null || !packageChoices.ContainsKey(freelancerId))
                {
                    // The client did not choose. We did, on their behalf, and
                    // that is reported rather than passed off as their decision.
                    fallbacks.Add(freelancerId);
                }

                var created = await invitations.CreateInvitationAsync(
                    approverId,
                    new CreateInvitationRequest
                    {
                        ProjectId = projectId,
                        FreelancerId = freelancerId,
                        PackageId = packageId.Value,
                    });
                invitationIds.Add(created.Id);
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or UnauthorizedAccessException)
            {
                // Business-rule rejections, reported rather than thrown. Anything
                // else is a genuine fault and is left to propagate.
                skipped.Add((freelancerId, ex.Message));
            }
        }

        if (invitationIds.Count == 0)
        {
            // Every approved freelancer was rejected. That is a failed run, not a
            // partial success: the client asked to contact people and nobody was
            // contacted.
            run.Status = WorkflowRunStatus.Failed;
        }
        else if (skipped.Count > 0)
        {
            run.Stage = WorkflowStages.Outreach;
        }

        run.FinalOutput = JsonSerializer.Serialize(new
        {
            invited = invitationIds,
            skipped,
        });
        await _db.SaveChangesAsync(ct);

        return new ShortlistApprovalResult(run, invitationIds, skipped, fallbacks);
    }

    /// <summary>
    /// The freelancer's lowest-priced package, or null if they have none.
    ///
    /// Lowest rather than first, because an invitation commits the client to a
    /// price and the cheapest package is the one least likely to be a surprise.
    /// `PackageId` is generated by the database and the shortlist response carries
    /// prices but not ids, so the id has to be looked up here.
    /// </summary>
    private async Task<Guid?> ResolveDefaultPackageIdAsync(Guid freelancerId, CancellationToken ct)
    {
        var userId = await _db.FreelancerProfiles
            .Where(p => p.Id == freelancerId)
            .Select(p => (Guid?)p.UserId)
            .FirstOrDefaultAsync(ct);

        if (userId is null)
        {
            return null;
        }

        return await _db.FreelancerPackages
            .Where(pk => pk.UserId == userId.Value)
            .OrderBy(pk => pk.Price)
            .Select(pk => (Guid?)pk.Id)
            .FirstOrDefaultAsync(ct);
    }

    /// <summary>The decision recorded by a shortlist approval, as stored in <c>ResumeValue</c>.</summary>
    private sealed class ShortlistApproval
    {
        public List<Guid> FreelancerIds { get; set; } = new();
        public Guid ApprovedBy { get; set; }
        public DateTime ApprovedAt { get; set; }
    }

    /// <summary>
    /// Resumes a paused run from the recorded human decision.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This comment previously said "the graph is checkpointed on the Python side
    /// keyed by <c>workflow_run_id</c>". That was false, and it was the most
    /// expensive kind of wrong: it asserted exactly the dual-ownership
    /// architecture ADR 0003 was written to reject. Anyone reasoning about pause
    /// state who started here — which is where a reader starts, since this is the
    /// method that implements resuming — would conclude there were two stores for
    /// the same fact.
    ///
    /// There is no Python-side checkpoint. <c>ai/graph/dispatcher.py</c> holds no
    /// state across requests, and <c>run.ResumeValue</c> is the durable record.
    /// </para>
    /// <para>
    /// The one thing the old comment got right, and which is worth keeping, is
    /// <em>why</em> this is a second dispatch rather than a re-run from the start:
    /// recomputing would produce a different shortlist from the one the client
    /// approved. That is the property that makes the approval mean something.
    /// </para>
    /// </remarks>
    public async Task<WorkflowRun> ResumeWorkflowAsync(
        int workflowRunId,
        Guid userId,
        string? resumeValue = null,
        CancellationToken ct = default)
    {
        var run = await _db.WorkflowRuns
            .FirstOrDefaultAsync(x => x.Id == workflowRunId, ct)
            ?? throw new KeyNotFoundException($"Workflow run {workflowRunId} was not found.");

        // Ownership, checked before anything else happens.
        //
        // This method accepted `userId` and never used it, so any authenticated
        // client could resume any other client's paused run: run ids are
        // sequential integers, so it was a matter of editing a number in the URL.
        // The effect was not just a cross-tenant status write -- `TryStartPythonAgentAsync`
        // below re-dispatches an LLM agent with this run's project context and the
        // stored decision, so it handed one tenant's project to another tenant's
        // token and billed the call against their workflow.
        //
        // `CancelWorkflowAsync` immediately below already does this, and
        // `AiTalentMatchController.Shortlist` does it on the read path. Resuming was
        // the only mutation of the three that did not.
        if (run.RequestedByUserId != userId)
            throw new UnauthorizedAccessException("Only the requesting user can resume this workflow.");

        if (run.Status is not (WorkflowRunStatus.PendingClientApproval or WorkflowRunStatus.PendingManagerApproval))
            throw new InvalidOperationException(
                $"Workflow {workflowRunId} is not paused awaiting approval (status {run.Status}).");

        // The stored decision is authoritative. An explicit argument is only
        // consulted when the run has nothing recorded, so a client re-sending a
        // stale body cannot overwrite a decision already made.
        var value = run.ResumeValue ?? resumeValue;
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException(
                $"Workflow {workflowRunId} has no recorded decision to resume from.");

        run.Status = WorkflowRunStatus.Running;
        run.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        // Parsed to a JSON object, not forwarded as the raw column text. The agent
        // reads `resume_value` as a dict and finds `approved = []` if it arrives
        // quoted, so the outreach stage would contact nobody while reporting that
        // it had.
        await TryStartPythonAgentAsync(
            run,
            new { resume = true, resume_value = AsJsonValue(value) },
            ct);
        return run;
    }

    public async Task<WorkflowRun> CancelWorkflowAsync(int workflowRunId, Guid requestedByUserId, CancellationToken ct = default)
    {
        var run = await _db.WorkflowRuns
            .FirstOrDefaultAsync(x => x.Id == workflowRunId, ct)
            ?? throw new KeyNotFoundException($"Workflow run {workflowRunId} was not found.");

        if (run.RequestedByUserId != requestedByUserId)
            throw new UnauthorizedAccessException("Only the requesting user can cancel this workflow.");

        if (run.Status != WorkflowRunStatus.Pending && run.Status != WorkflowRunStatus.Running)
            throw new InvalidOperationException("Only pending or running workflows can be cancelled.");

        run.Status = WorkflowRunStatus.Cancelled;
        run.ApprovalDecision = "Cancelled";
        run.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return run;
    }

    public async Task<WorkflowStepLog> AppendStepLogAsync(int workflowRunId, string stepName, string? agentName, string? input, string? output, string status, long durationMs, CancellationToken ct = default)
    {
        var workflow = await _db.WorkflowRuns
            .FirstOrDefaultAsync(x => x.Id == workflowRunId, ct)
            ?? throw new KeyNotFoundException($"Workflow run {workflowRunId} was not found.");

        var log = new WorkflowStepLog
        {
            WorkflowRunId = workflow.Id,
            StepName = stepName,
            AgentName = agentName,
            Input = SerializeJsonbString(input),
            Output = SerializeJsonbString(output),
            Status = status,
            DurationMs = durationMs,
            CreatedAt = DateTime.UtcNow
        };

        _db.WorkflowStepLogs.Add(log);
        ApplyStepStatus(workflow, status);
        workflow.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return log;
    }

    public async Task<ToolCallLog> AppendToolCallLogAsync(int workflowRunId, string toolName, string? input, string? output, bool success, long durationMs, CancellationToken ct = default)
    {
        await EnsureWorkflowAsync(workflowRunId, ct);
        var log = new ToolCallLog
        {
            WorkflowRunId = workflowRunId,
            ToolName = toolName,
            Input = SerializeJsonbString(input),
            Output = SerializeJsonbString(output),
            Success = success,
            DurationMs = durationMs,
            CreatedAt = DateTime.UtcNow
        };
        _db.ToolCallLogs.Add(log);
        await _db.SaveChangesAsync(ct);
        return log;
    }

    public async Task<AgentRecommendation> AppendAgentRecommendationAsync(int workflowRunId, AgentRecommendation recommendation, CancellationToken ct = default)
    {
        await EnsureWorkflowAsync(workflowRunId, ct);
        recommendation.WorkflowRunId = workflowRunId;
        recommendation.FactorsJson = SerializeJsonbString(recommendation.FactorsJson);
        recommendation.PortfolioSamplesJson = SerializeJsonbString(recommendation.PortfolioSamplesJson);
        _db.AgentRecommendations.Add(recommendation);
        await _db.SaveChangesAsync(ct);
        return recommendation;
    }

    public async Task<ProposalScreening> AppendProposalScreeningAsync(int workflowRunId, ProposalScreening screening, CancellationToken ct = default)
    {
        await EnsureWorkflowAsync(workflowRunId, ct);
        screening.WorkflowRunId = workflowRunId;
        screening.StrengthsJson = SerializeJsonbString(screening.StrengthsJson);
        screening.ConcernsJson = SerializeJsonbString(screening.ConcernsJson);
        _db.ProposalScreenings.Add(screening);
        await _db.SaveChangesAsync(ct);
        return screening;
    }

    public async Task<ValidationResult> AppendValidationResultAsync(int workflowRunId, ValidationResult result, CancellationToken ct = default)
    {
        await EnsureWorkflowAsync(workflowRunId, ct);
        result.WorkflowRunId = workflowRunId;
        result.IssuesJson = SerializeJsonbString(result.IssuesJson);
        result.CostBreakdownJson = SerializeJsonbString(result.CostBreakdownJson);
        _db.ValidationResults.Add(result);
        await _db.SaveChangesAsync(ct);
        return result;
    }

    /// <param name="status">
    /// Where to leave the run. Null means Completed.
    /// </param>
    /// <remarks>
    /// <para>
    /// This used to hardcode <see cref="WorkflowRunStatus.Completed"/>, which is
    /// wrong for the ordinary end of a talent-match run: that stage finishes by
    /// <i>waiting for a person</i>, and Completed says there is nothing left to
    /// do. A run parked on the client-approval gate is not complete.
    /// </para>
    /// <para>
    /// It mattered because the status is what everything downstream reads.
    /// <c>AiTalentMatchController.Start</c> de-duplicates on
    /// <c>status IN (0, 1, 9)</c>, so a paused run left at <c>Pending</c> is
    /// handed back to the client forever instead of being re-dispatched, and the
    /// shortlist page renders "pending" over a run that is actually waiting for
    /// approval. Observed on run 24.
    /// </para>
    /// <para>
    /// An unrecognised status is ignored rather than throwing: the caller's job
    /// is to record output, and refusing to record it because of a label is how a
    /// good result gets lost. The step log still carries the status either way.
    /// </para>
    /// </remarks>
    public async Task<WorkflowRun> CompleteWorkflowAsync(int workflowRunId, string? output, string? status = null, CancellationToken ct = default)
    {
        var workflow = await _db.WorkflowRuns.FirstOrDefaultAsync(x => x.Id == workflowRunId, ct)
            ?? throw new KeyNotFoundException($"Workflow run {workflowRunId} was not found.");
        workflow.Status = WorkflowRunStatus.Completed;
        if (!string.IsNullOrWhiteSpace(status)
            && Enum.TryParse<WorkflowRunStatus>(status, true, out var parsed))
        {
            workflow.Status = parsed;
        }

        workflow.FinalOutput = output;
        workflow.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return workflow;
    }

    private async Task<WorkflowRun> EnsureWorkflowAsync(int workflowRunId, CancellationToken ct)
        => await _db.WorkflowRuns.FirstOrDefaultAsync(x => x.Id == workflowRunId, ct)
            ?? throw new KeyNotFoundException($"Workflow run {workflowRunId} was not found.");

    private static void ApplyStepStatus(WorkflowRun workflow, string status)
    {
        if (Enum.TryParse<WorkflowRunStatus>(status, true, out var parsed))
            workflow.Status = parsed;
    }

    private static WorkflowContractSpec? TryReadContractSpec(string? plan)
    {
        if (string.IsNullOrWhiteSpace(plan)) return null;
        try
        {
            using var document = JsonDocument.Parse(plan);
            return document.RootElement.ValueKind == JsonValueKind.Object
                ? document.Deserialize<WorkflowContractSpec>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Pulls the planner's plan out of an agent response, as JSON text.
    /// </summary>
    /// <remarks>
    /// The agent service answers <c>{ status, stage, summary, payload }</c> and
    /// puts the plan at <c>payload.plan</c>. Extracted here rather than
    /// deserialising the whole envelope because the payload shape is the agent
    /// service's to change, and a rename should cost one lost weight display
    /// rather than a failed run.
    ///
    /// Returns null for anything unparseable, including a failure envelope where
    /// there is no payload. Never throws: this runs on the success path of a run
    /// that has already succeeded, and failing it over a cosmetic field would
    /// discard the result.
    /// </remarks>
    internal static string? ExtractPlanFromAgentResponse(string? body)
    {
        if (string.IsNullOrWhiteSpace(body)) return null;

        try
        {
            using var document = JsonDocument.Parse(body);
            if (document.RootElement.ValueKind != JsonValueKind.Object) return null;

            if (!document.RootElement.TryGetProperty("payload", out var payload)
                || payload.ValueKind != JsonValueKind.Object)
                return null;

            if (!payload.TryGetProperty("plan", out var plan)
                || plan.ValueKind != JsonValueKind.Object)
                return null;

            return plan.GetRawText();
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private async Task TryStartPythonAgentAsync(WorkflowRun run, object? context, CancellationToken ct)
    {
        await TryStartPythonAgentAsync(run, context, ct, plan: null);
    }

    /// <summary>
    /// Dispatches a run to the agent service.
    /// </summary>
    /// <param name="plan">
    /// The run's plan, sent at the top level as well as inside <paramref name="context"/>.
    /// </param>
    /// <remarks>
    /// The agent request model reads the plan from the top-level <c>plan</c> field;
    /// nothing in this service read it from <c>context</c>. A plan supplied by a
    /// client was therefore written to <c>WorkflowRun.Plan</c>, echoed into
    /// <c>context.plan</c>, and never read -- so "adjust the weights and re-run"
    /// ran the planner anyway. The run succeeded, produced a plausible shortlist,
    /// and reported the planner's weights, with nothing anywhere saying the
    /// override had been discarded.
    /// </remarks>
    private async Task TryStartPythonAgentAsync(
        WorkflowRun run, object? context, CancellationToken ct, string? plan)
    {
        if (_httpClientFactory is null || _aiOptions is null || string.IsNullOrWhiteSpace(_aiOptions.BaseUrl))
            return;

        try
        {
            var client = _httpClientFactory.CreateClient("AgenticAi");
            using var request = new HttpRequestMessage(HttpMethod.Post, "agent/chat")
            {
                // `stage` and `context` are required by the agent service's
                // dispatch contract. The call previously sent neither, so the
                // service had no way to tell a draft from a shortlist and no
                // project id to work from -- which is why the AI surface has never
                // produced a result.
                Content = JsonContent.Create(new
                {
                    message = run.Objective,
                    execution_mode = "recorded_replay",
                    workflow_run_id = run.Id,
                    stage = run.Stage,
                    project_id = run.ProjectId,
                    context,
                    // Sent top-level, where the agent reads it, and as a JSON
                    // object rather than as the column's text. See
                    // `TryStartPythonAgentAsync` for why the omission of this was
                    // silent.
                    plan = AsJsonValue(plan),
                })
            };
            if (!string.IsNullOrWhiteSpace(_aiOptions.InternalApiKey))
                request.Headers.Add("X-Internal-Api-Key", _aiOptions.InternalApiKey);

            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
            if (!response.IsSuccessStatusCode)
                return;

            run.FinalOutput = await response.Content.ReadAsStringAsync(ct);

            // Persist the *effective* plan, whichever way it was decided.
            //
            // `run.Plan` holds what the caller asked for, which is null on an
            // ordinary first run -- the planner runs inside the agent service and
            // its weights come back only in the response body. So without this,
            // a client that never touched a slider has nothing to display, and
            // the "adjust the weights" control would have nothing to adjust.
            //
            // Written only when the run has no plan of its own, so a client's
            // chosen numbers are never overwritten by the agent's echo of them.
            if (string.IsNullOrWhiteSpace(run.Plan))
            {
                var planned = ExtractPlanFromAgentResponse(run.FinalOutput);
                if (planned is not null)
                    run.Plan = planned;
            }

            // Park on the gate the stage declares, rather than a single generic
            // "pending approval". A stage with no gate is finished as far as this
            // service is concerned; the agent writes its artefacts through the
            // internal callback and marks it complete.
            run.Status = WorkflowStages.ApprovalGateFor(run.Stage) ?? WorkflowRunStatus.Completed;
            run.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
        }
        catch (HttpRequestException)
        {
            // The workflow remains persisted and can be completed through the internal callback path.
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            // The bounded AI call timed out; leave the durable workflow pending.
        }
    }
}
