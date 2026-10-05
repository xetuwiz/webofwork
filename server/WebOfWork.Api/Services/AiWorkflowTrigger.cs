using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WebOfWork.Api.Data;
using WebOfWork.Api.Models;

namespace WebOfWork.Api.Services;

/// <summary>
/// Starts AI workflow runs from inside existing business flows.
/// </summary>
/// <remarks>
/// <para>
/// The `draft` and `talent_match` stages are started by a controller, because a
/// person asked for them. `screen` and `validate` have no such person: they are
/// consequences of something else happening -- a proposal arriving, a payment
/// being verified -- and until something started them, both stages were dead.
/// The dispatcher understood them, the validators accepted them, the database had
/// tables for their output, and no code path could ever create a run. That also
/// made the manager approval gate unreachable, because a run only enters
/// <c>PendingManagerApproval</c> from a validate-stage run.
/// </para>
/// <para>
/// <b>Nothing here is allowed to affect the flow that triggered it.</b> A
/// freelancer submitting a proposal and a client paying an invoice are the two
/// most important operations in the product, and neither may fail, slow down, or
/// change shape because an LLM service is cold, rate-limited, or down. So:
/// </para>
/// <list type="number">
/// <item>the run is created and committed, so it survives a crash;</item>
/// <item>the agent call happens after the response has been written, and is not
/// awaited by the caller's request;</item>
/// <item>every failure is logged and swallowed -- a proposal is still a
/// proposal.</item>
/// </list>
/// <para>
/// The cost of fail-open is that a run can sit in <c>Pending</c> with no result.
/// That is visible and recoverable -- an operator can dispatch it again, since
/// the run is durable and the agents are idempotent per run -- and it is the
/// better failure than taking down the payment path.
/// </para>
/// </remarks>
public interface IAiWorkflowTrigger
{
    /// <summary>
    /// Runs the screening stage for a project that has just received proposals.
    /// </summary>
    /// <returns>The run, or null if one already existed or nothing was started.</returns>
    Task<WorkflowRun?> ScreenProposalsAsync(int projectId, Guid requestedByUserId, CancellationToken ct = default);

    /// <summary>
    /// Runs the validation stage before a contract is created, which is what puts
    /// a run into the manager approval gate.
    /// </summary>
    Task<WorkflowRun?> ValidateBeforeContractAsync(int projectId, Guid requestedByUserId, CancellationToken ct = default);
}

public class AiWorkflowTrigger : IAiWorkflowTrigger
{
    private readonly IAiWorkflowService _workflows;
    private readonly WebOfWorkDbContext _db;
    private readonly ILogger<AiWorkflowTrigger>? _log;
    private readonly Microsoft.Extensions.DependencyInjection.IServiceScopeFactory _scopeFactory;

    public AiWorkflowTrigger(
        IAiWorkflowService workflows,
        WebOfWorkDbContext db,
        Microsoft.Extensions.DependencyInjection.IServiceScopeFactory scopeFactory,
        ILogger<AiWorkflowTrigger>? log = null)
    {
        _workflows = workflows;
        _db = db;
        _scopeFactory = scopeFactory;
        _log = log;
    }

    public Task<WorkflowRun?> ScreenProposalsAsync(int projectId, Guid requestedByUserId, CancellationToken ct = default)
        => TriggerOnceAsync(
            WorkflowStages.Screen,
            projectId,
            requestedByUserId,
            objective: $"Screen the proposals on project #{projectId}",
            ct: ct);

    public Task<WorkflowRun?> ValidateBeforeContractAsync(int projectId, Guid requestedByUserId, CancellationToken ct = default)
        => TriggerOnceAsync(
            WorkflowStages.Validate,
            projectId,
            requestedByUserId,
            objective: $"Validate project #{projectId} before a contract is created",
            ct: ct);

    /// <summary>
    /// Creates at most one run per project and stage, then dispatches it
    /// without blocking.
    /// </summary>
    private async Task<WorkflowRun?> TriggerOnceAsync(
        string stage, int projectId, Guid requestedByUserId, string objective, CancellationToken ct)
    {
        try
        {
            var alreadyRunning = await _db.WorkflowRuns
                .IgnoreQueryFilters()
                .AsNoTracking()
                .AnyAsync(r => r.ProjectId == projectId
                               && r.Stage == stage
                               && r.Status != WorkflowRunStatus.Failed
                               && r.Status != WorkflowRunStatus.Cancelled
                               && r.Status != WorkflowRunStatus.Rejected, ct);

            if (alreadyRunning)
            {
                _log?.LogDebug(
                    "Not starting a {Stage} run for project {ProjectId}: one already exists.",
                    stage, projectId);
                return null;
            }

            var run = await _workflows.StartWorkflowDeferredAsync(
                requestedByUserId, objective, stage, projectId,
                context: new { project_id = projectId }, ct: ct);

            // Detached, and not awaited. `Task.Run` rather than a bare call so
            // the agent request does not run inline on the caller's continuation,
            // which would put it back on the request path in everything but name.
            //
            // The run is already committed, so the worst outcome of a lost
            // dispatch is a `Pending` run with no output, which an operator can
            // re-dispatch. The worst outcome of awaiting here is a freelancer
            // whose proposal submission hangs on a cold model.
            _ = Task.Run(async () =>
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var scopedWorkflows = scope.ServiceProvider.GetRequiredService<IAiWorkflowService>();
                    await scopedWorkflows.DispatchAsync(run.Id, new { project_id = projectId }, CancellationToken.None);
                }
                catch (Exception ex)
                {
                    // Never propagates. A proposal and a payment are recorded
                    // whether or not the agent service is reachable.
                    _log?.LogWarning(ex,
                        "Dispatch of {Stage} run {RunId} failed. The run is still "
                        + "recorded and can be dispatched again.",
                        stage, run.Id);
                }
            });

            _log?.LogInformation(
                "Started {Stage} run {RunId} for project {ProjectId}.", stage, run.Id, projectId);
            return run;
        }
        catch (Exception ex)
        {
            // A failure to *start* the run is still not allowed to fail the
            // proposal or the payment.
            _log?.LogError(ex,
                "Could not start a {Stage} run for project {ProjectId}. The triggering "
                + "operation continues unaffected.", stage, projectId);
            return null;
        }
    }
}
