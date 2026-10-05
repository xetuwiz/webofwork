using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using WebOfWork.Api.Data;
using WebOfWork.Api.Models;
using WebOfWork.Api.Models.Enums;
using WebOfWork.Api.Services;
using Xunit;

namespace WebOfWork.Api.Tests.AiWorkflow;

/// <summary>
/// The stages that start themselves.
/// </summary>
/// <remarks>
/// <para>
/// <c>draft</c> and <c>talent_match</c> are started by a controller, because a
/// person asked for them. <c>screen</c> and <c>validate</c> have no such person --
/// they are consequences of a proposal arriving and of a payment being verified.
/// Until <see cref="AiWorkflowTrigger"/> existed, nothing started them: the
/// dispatcher understood both stages, the validators accepted both, the database
/// had tables for their output, and no code path could create a run for either.
/// </para>
/// <para>
/// That also left the manager approval gate unreachable, because a run only
/// enters <c>PendingManagerApproval</c> from a validate-stage run. So the whole
/// second half of the two-gate design depended on a missing line of wiring.
/// </para>
/// <para>
/// The second property, and the one that constrains the design: a proposal and a
/// payment are the two operations a user is least willing to lose, so neither may
/// fail, slow down, or change shape because an LLM service is cold or down.
/// </para>
/// </remarks>
public class AiWorkflowTriggerTests
{
    /// <summary>
    /// A workflow service that records what it was asked and can be told to fail.
    /// </summary>
    /// <remarks>
    /// It writes the run into the <see cref="WebOfWorkDbContext"/>, because the
    /// real one does and the trigger's duplicate check reads from there. A fake
    /// that returned an object without persisting it would make every
    /// duplicate-suppression assertion fail for the wrong reason -- which is how
    /// a test ends up asserting something other than what it says.
    /// </remarks>
    private sealed class RecordingWorkflows : IAiWorkflowService
    {
        private readonly WebOfWorkDbContext _db;

        public RecordingWorkflows(WebOfWorkDbContext db) => _db = db;

        public List<(string Stage, int? ProjectId, Guid UserId)> Started { get; } = new();
        public List<int> Dispatched { get; } = new();
        public bool ThrowOnStart { get; set; }
        public bool ThrowOnDispatch { get; set; }

        public Task<WorkflowRun> StartWorkflowAsync(
            Guid requestedByUserId, string objective, string stage, string? plan = null,
            int? projectId = null, object? context = null, CancellationToken ct = default,
            bool dispatchToAgent = true)
            => throw new NotSupportedException(
                "The trigger must use the deferred path, or a proposal submission "
                + "would block on the agent service.");

        public async Task<WorkflowRun> StartWorkflowDeferredAsync(
            Guid requestedByUserId, string objective, string stage, int? projectId = null,
            object? context = null, CancellationToken ct = default)
        {
            if (ThrowOnStart) throw new InvalidOperationException("agent service is down");

            var run = new WorkflowRun
            {
                Objective = objective,
                Stage = stage,
                ProjectId = projectId,
                RequestedByUserId = requestedByUserId,
                Status = WorkflowRunStatus.Pending,
            };

            _db.WorkflowRuns.Add(run);
            await _db.SaveChangesAsync(ct);

            Started.Add((stage, projectId, requestedByUserId));
            return run;
        }

        public async Task DispatchAsync(int workflowRunId, object? context = null, CancellationToken ct = default)
        {
            if (ThrowOnDispatch) throw new InvalidOperationException("agent service is down");
            Dispatched.Add(workflowRunId);
            await Task.Yield();
        }

        // Unused by the trigger. Throwing keeps an accidental new dependency
        // visible at review rather than silently defaulting.
        public Task<WorkflowRun> ResumeWorkflowAsync(int id, Guid u, string v, CancellationToken c = default) => throw new NotSupportedException();
        public Task<ShortlistApprovalResult> ApproveShortlistAsync(int id, Guid u, IReadOnlyCollection<Guid> ids, string? r, IReadOnlyDictionary<Guid, Guid>? p = null, CancellationToken c = default) => throw new NotSupportedException();
        public Task<WorkflowRun> ApproveWorkflowAsync(int id, Guid u, string? d = null, string? r = null, WorkflowContractSpec? s = null, CancellationToken c = default) => throw new NotSupportedException();
        public Task<WorkflowRun> CancelWorkflowAsync(int id, Guid u, CancellationToken c = default) => throw new NotSupportedException();
        public Task<WorkflowStepLog> AppendStepLogAsync(int id, string s, string? a, string? i, string? o, string st, long d, CancellationToken c = default) => throw new NotSupportedException();
        public Task<ToolCallLog> AppendToolCallLogAsync(int id, string t, string? i, string? o, bool s, long d, CancellationToken c = default) => throw new NotSupportedException();
        public Task<AgentRecommendation> AppendAgentRecommendationAsync(int id, AgentRecommendation r, CancellationToken c = default) => throw new NotSupportedException();
        public Task<ProposalScreening> AppendProposalScreeningAsync(int id, ProposalScreening s, CancellationToken c = default) => throw new NotSupportedException();
        public Task<ValidationResult> AppendValidationResultAsync(int id, ValidationResult v, CancellationToken c = default) => throw new NotSupportedException();
        public Task<WorkflowRun> CompleteWorkflowAsync(int id, string? o, string? status = null, CancellationToken c = default) => throw new NotSupportedException();
    }

    private static WebOfWorkDbContext EmptyDb(string name)
    {
        // An in-memory provider is enough: every assertion here is about the
        // duplicate check and the failure handling, neither of which touches SQL.
        var options = new DbContextOptionsBuilder<WebOfWorkDbContext>()
            .UseInMemoryDatabase(name)
            .Options;
        return new WebOfWorkDbContext(options);
    }

    /// <summary>
    /// Builds a trigger whose scoped <see cref="IAiWorkflowService"/> resolves to
    /// <paramref name="workflows"/>.
    ///
    /// The trigger dispatches through <c>_scopeFactory.CreateScope()</c> rather than
    /// through the injected service, so it needs a scope factory from the moment the
    /// dependency was introduced. Handing it a loose <c>Mock&lt;IServiceScopeFactory&gt;</c>
    /// would compile and leave every test green, because the dispatch sits inside a
    /// <c>catch (Exception)</c> that never propagates -- a null scope would be
    /// swallowed and the fake would never record a dispatch. That is a test that
    /// passes because the thing it means to exercise threw. A real scope over the
    /// same fake keeps the assertions honest.
    /// </summary>
    private static AiWorkflowTrigger Trigger(IAiWorkflowService workflows, WebOfWorkDbContext db)
    {
        var services = new ServiceCollection();
        services.AddSingleton(workflows);
        var provider = services.BuildServiceProvider();
        return new AiWorkflowTrigger(workflows, db, provider.GetRequiredService<IServiceScopeFactory>());
    }

    [Fact]
    public async Task AProposal_StartsExactlyOneScreenRun()
    {
        await using var db = EmptyDb(nameof(AProposal_StartsExactlyOneScreenRun));
        var workflows = new RecordingWorkflows(db);
        var trigger = Trigger(workflows, db);

        var first = await trigger.ScreenProposalsAsync(projectId: 42, requestedByUserId: Guid.NewGuid());
        // A second proposal on the same project must not start a second run.
        var second = await trigger.ScreenProposalsAsync(projectId: 42, requestedByUserId: Guid.NewGuid());

        first.Should().NotBeNull("the first proposal has nothing to screen against");
        second.Should().BeNull("a screen run already exists for this project");

        workflows.Started.Should().ContainSingle();
        workflows.Started[0].Stage.Should().Be(WorkflowStages.Screen);
        workflows.Started[0].ProjectId.Should().Be(42);
    }

    [Fact]
    public async Task TwoDifferentProjects_EachGetTheirOwnScreenRun()
    {
        await using var db = EmptyDb(nameof(TwoDifferentProjects_EachGetTheirOwnScreenRun));
        var workflows = new RecordingWorkflows(db);
        var trigger = Trigger(workflows, db);

        await trigger.ScreenProposalsAsync(1, Guid.NewGuid());
        await trigger.ScreenProposalsAsync(2, Guid.NewGuid());

        workflows.Started.Should().HaveCount(2);
        workflows.Started.Select(s => s.ProjectId).Should().BeEquivalentTo([1, 2]);
    }

    [Fact]
    public async Task AFailedRun_DoesNotBlockANewOne()
    {
        await using var db = EmptyDb(nameof(AFailedRun_DoesNotBlockANewOne));
        var workflows = new RecordingWorkflows(db);
        var trigger = Trigger(workflows, db);

        // A run that failed, as a previous agent crash would leave behind.
        db.WorkflowRuns.Add(new WorkflowRun
        {
            Objective = "previous", Stage = WorkflowStages.Screen, ProjectId = 7,
            Status = WorkflowRunStatus.Failed, RequestedByUserId = Guid.NewGuid(),
        });
        await db.SaveChangesAsync();

        (await trigger.ScreenProposalsAsync(7, Guid.NewGuid())).Should().NotBeNull(
            "a failed run records nothing, so the stage would never run again for "
            + "this project");

        workflows.Started.Should().ContainSingle();
    }

    [Fact]
    public async Task AnAlreadyCancelledRun_DoesNotBlockANewOne()
    {
        await using var db = EmptyDb(nameof(AnAlreadyCancelledRun_DoesNotBlockANewOne));
        var trigger = Trigger(new RecordingWorkflows(db), db);

        db.WorkflowRuns.Add(new WorkflowRun
        {
            Objective = "previous", Stage = WorkflowStages.Screen, ProjectId = 8,
            Status = WorkflowRunStatus.Cancelled, RequestedByUserId = Guid.NewGuid(),
        });
        await db.SaveChangesAsync();

        (await trigger.ScreenProposalsAsync(8, Guid.NewGuid())).Should().NotBeNull();
    }

    [Fact]
    public async Task AVerifiedPayment_StartsTheRunThatOpensTheManagerGate()
    {
        await using var db = EmptyDb(nameof(AVerifiedPayment_StartsTheRunThatOpensTheManagerGate));
        var workflows = new RecordingWorkflows(db);
        var trigger = Trigger(workflows, db);

        await trigger.ValidateBeforeContractAsync(99, Guid.NewGuid());

        workflows.Started.Should().ContainSingle();
        workflows.Started[0].Stage.Should().Be(WorkflowStages.Validate);

        // The whole reason this exists. A validate-stage run is the *only* thing
        // that can put a run into `PendingManagerApproval`, so if the validate
        // stage is never started the second approval gate is unreachable.
        WorkflowStages.ApprovalGateFor(WorkflowStages.Validate)
            .Should().Be(WorkflowRunStatus.PendingManagerApproval);
    }

    [Fact]
    public async Task AFailedAgentService_DoesNotPropagate()
    {
        await using var db = EmptyDb(nameof(AFailedAgentService_DoesNotPropagate));
        var workflows = new RecordingWorkflows(db) { ThrowOnStart = true };
        var trigger = Trigger(workflows, db);

        // A freelancer submitting a proposal must not get an error because the
        // AI service is unreachable. The proposal is already committed; this
        // call is a side effect on top of it.
        var run = await trigger.ScreenProposalsAsync(1, Guid.NewGuid());

        run.Should().BeNull();
    }

    [Fact]
    public async Task AFailedDispatch_DoesNotPropagate()
    {
        await using var db = EmptyDb(nameof(AFailedDispatch_DoesNotPropagate));
        var workflows = new RecordingWorkflows(db) { ThrowOnDispatch = true };
        var trigger = Trigger(workflows, db);

        // The run is created, the agent call fails afterwards. Still no throw.
        var run = await trigger.ScreenProposalsAsync(1, Guid.NewGuid());

        run.Should().NotBeNull("the run is durable, so it can be dispatched again");
    }

    [Fact]
    public async Task AStartedRun_IsDispatched_AndTheResultIsNotAwaited()
    {
        await using var db = EmptyDb(nameof(AStartedRun_IsDispatched_AndTheResultIsNotAwaited));
        var workflows = new RecordingWorkflows(db);
        var trigger = Trigger(workflows, db);

        await trigger.ScreenProposalsAsync(1, Guid.NewGuid());

        // The dispatch is deliberately detached, so it may not have run by the
        // time this assertion executes. Waiting for it is how a test proves the
        // call was *made* rather than left pending forever -- a `Task.Run` that
        // silently never ran would otherwise pass unnoticed.
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (workflows.Dispatched.Count == 0 && DateTime.UtcNow < deadline)
        {
            await Task.Delay(20);
        }

        workflows.Dispatched.Should().ContainSingle();
    }

    [Fact]
    public async Task ARunWhoseRequesterIsHidden_StillSuppressesADuplicate()
    {
        await using var db = EmptyDb(nameof(ARunWhoseRequesterIsHidden_StillSuppressesADuplicate));
        var workflows = new RecordingWorkflows(db);
        var trigger = Trigger(workflows, db);

        // A run with a requester the soft-delete query filter cannot see. The
        // filter on `WorkflowRun` is `!RequestedByUser.IsDeleted`, and it
        // navigates -- so a row whose user is absent reads as deleted.
        //
        // The trigger uses `IgnoreQueryFilters`, so this row still counts. Without
        // that, a requester who has been soft-deleted would be re-triggered on
        // every subsequent proposal, producing a new run each time.
        db.WorkflowRuns.Add(new WorkflowRun
        {
            Objective = "earlier", Stage = WorkflowStages.Screen, ProjectId = 11,
            Status = WorkflowRunStatus.Pending, RequestedByUserId = Guid.NewGuid(),
        });
        await db.SaveChangesAsync();

        (await trigger.ScreenProposalsAsync(11, Guid.NewGuid())).Should().BeNull(
            "the stage has already run for this project, regardless of whether "
            + "the original requester is still visible");
    }

    [Fact]
    public async Task TwoStagesOnOneProject_DoNotCollide()
    {
        await using var db = EmptyDb(nameof(TwoStagesOnOneProject_DoNotCollide));
        var workflows = new RecordingWorkflows(db);
        var trigger = Trigger(workflows, db);

        // Screening and validation are different stages over the same project.
        // The duplicate check is per (project, stage), so neither suppresses the
        // other.
        (await trigger.ScreenProposalsAsync(5, Guid.NewGuid())).Should().NotBeNull();
        (await trigger.ValidateBeforeContractAsync(5, Guid.NewGuid())).Should().NotBeNull();

        workflows.Started.Select(s => s.Stage)
            .Should().BeEquivalentTo([WorkflowStages.Screen, WorkflowStages.Validate]);
    }
}
