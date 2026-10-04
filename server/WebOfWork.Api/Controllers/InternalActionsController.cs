using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebOfWork.Api.Data;
using WebOfWork.Api.Models;
using WebOfWork.Api.Services;

namespace WebOfWork.Api.Controllers;

/// <summary>
/// Write actions the AI service can take, as distinct from the read endpoints in
/// <c>InternalReadController</c>.
/// </summary>
/// <remarks>
/// <para>
/// The agent service gets its own surface for side effects rather than being given
/// a normal user JWT. Two reasons: an agent has no human identity to issue tokens
/// for, and its blast radius has to be bounded by something explicit. A prompt
/// injection cannot read the whole database, and it cannot compose an arbitrary
/// request -- it can only call the verbs listed here, each of which re-checks its
/// own preconditions against the database.
/// </para>
/// <para>
/// Auth is the <c>X-Internal-Api-Key</c> middleware, registered before
/// <c>UseRouting</c>, so these actions are unreachable without it. There is
/// deliberately no <c>[Authorize]</c> policy here: the key is the authorisation,
/// and layering a user policy on top would suggest a user is involved.
/// </para>
/// </remarks>
[ApiController]
[Route("internal/actions")]
public class InternalActionsController : ControllerBase
{
    private readonly WebOfWorkDbContext _db;
    private readonly IInvitationService _invitations;
    private readonly IAiWorkflowService _workflows;
    private readonly IInternalReadAuditService _audit;

    public InternalActionsController(
        WebOfWorkDbContext db,
        IInvitationService invitations,
        IAiWorkflowService workflows,
        IInternalReadAuditService audit)
    {
        _db = db;
        _invitations = invitations;
        _workflows = workflows;
        _audit = audit;
    }

    /// <summary>
    /// Invites the freelancers a client approved, on the outreach agent's behalf.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every precondition is re-checked against the database rather than trusted
    /// from the request. The agent is not an authenticated principal in the sense
    /// that matters here -- it is a component whose inputs are model output -- so
    /// "the agent said the client approved this" is not evidence that they did.
    /// The durable record of that decision is the run's status and resume value,
    /// and this reads those.
    /// </para>
    /// <para>
    /// The requested freelancers must also appear in the run's
    /// <c>AgentRecommendation</c> rows. Without that, an agent could name anyone
    /// and the "only contact people the model shortlisted" property would be
    /// enforced only by the prompt.
    /// </para>
    /// <para>
    /// Failures are per-freelancer and reported in <c>skipped</c> rather than
    /// thrown. A shortlist of ten where two already proposed should still reach the
    /// other eight; only a wholesale failure returns an error status.
    /// </para>
    /// </remarks>
    /// <summary>
    /// The freelancer ids this run actually recommended.
    /// </summary>
    /// <remarks>
    /// Added for the validation agent's rule 10, which checks that an approved
    /// shortlist only contains people the run put forward. Without this endpoint
    /// the agent could only take the shortlist on trust -- which is precisely
    /// the thing the rule exists to verify.
    ///
    /// Ids only. The rule needs to know *who*, and returning the full
    /// recommendation would hand the agent the scores and reasons it is supposed
    /// to be checking independently of.
    /// </remarks>
    /// <summary>
    /// A freelancer's lowest-priced package id, or null if they have none.
    /// </summary>
    /// <remarks>
    /// Lowest rather than first: an invitation commits the client to a price, and
    /// the cheapest package is the one least likely to surprise either party.
    /// </remarks>
    private async Task<Guid?> ResolveDefaultPackageIdAsync(Guid freelancerId, CancellationToken ct)
    {
        var userId = await _db.FreelancerProfiles
            .Where(p => p.Id == freelancerId)
            .Select(p => (Guid?)p.UserId)
            .FirstOrDefaultAsync(ct);

        if (userId is null) return null;

        return await _db.FreelancerPackages
            .Where(pk => pk.UserId == userId.Value)
            .OrderBy(pk => pk.Price)
            .Select(pk => (Guid?)pk.Id)
            .FirstOrDefaultAsync(ct);
    }

    [HttpGet("workflows/{workflowRunId:int}/recommendations")]
    public async Task<IActionResult> GetRecommendedFreelancerIds(
        int workflowRunId,
        CancellationToken cancellationToken)
    {
        using var audit = _audit.Begin("workflow.recommendations", workflowRunId.ToString());

        var exists = await _db.WorkflowRuns.AsNoTracking()
            .AnyAsync(x => x.Id == workflowRunId, cancellationToken);

        if (!exists) return NotFound(new { error = $"Workflow run {workflowRunId} was not found." });

        var ids = await _db.AgentRecommendations.AsNoTracking()
            .Where(r => r.WorkflowRunId == workflowRunId)
            .Select(r => r.FreelancerId)
            .Distinct()
            .ToListAsync(cancellationToken);

        return Ok(new { workflow_run_id = workflowRunId, freelancer_ids = ids });
    }

    [HttpPost("send-invitations")]
    public async Task<IActionResult> SendInvitations(
        [FromBody] SendInvitationsRequest request,
        CancellationToken cancellationToken)
    {
        if (request.WorkflowRunId <= 0)
            return BadRequest(new { error = "workflow_run_id is required." });

        if (request.FreelancerIds.Count == 0)
            return BadRequest(new { error = "freelancer_ids must not be empty." });

        var run = await _db.WorkflowRuns
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == request.WorkflowRunId, cancellationToken);

        if (run is null)
            return NotFound(new { error = $"Workflow run {request.WorkflowRunId} was not found." });

        // The run must be approved and must be a run that can invite. Checking
        // `Stage` as well as `Status` is what stops an outreach call being made for
        // a screening run that happens to be marked approved.
        if (run.Status != WorkflowRunStatus.Approved)
            return Conflict(new
            {
                error = $"Workflow run {request.WorkflowRunId} is not approved (status {run.Status}).",
            });

        if (run.Stage != WorkflowStages.TalentMatch && run.Stage != WorkflowStages.Outreach)
            return Conflict(new
            {
                error = $"Stage '{run.Stage}' cannot send invitations. Expected '{WorkflowStages.TalentMatch}' or '{WorkflowStages.Outreach}'.",
            });

        var projectId = request.ProjectId > 0 ? request.ProjectId : run.ProjectId;
        if (projectId is null or <= 0)
            return BadRequest(new { error = "project_id is required, either in the request or on the run." });

        if (run.ProjectId.HasValue && run.ProjectId.Value != projectId)
            return BadRequest(new
            {
                error = "The request project does not match the workflow project.",
            });

        var project = await _db.Projects.AsNoTracking()
            .Include(x => x.Client)
            .FirstOrDefaultAsync(x => x.Id == projectId, cancellationToken);

        if (project is null)
            return NotFound(new { error = $"Project {projectId} was not found." });

        // The shortlist the agent is allowed to act on. Anything outside it is
        // rejected before an invitation is attempted.
        var shortlisted = await _db.AgentRecommendations
            .AsNoTracking()
            .Where(r => r.WorkflowRunId == request.WorkflowRunId)
            .Select(r => r.FreelancerId)
            .ToListAsync(cancellationToken);

        var shortlistedSet = new HashSet<Guid>(shortlisted);

        var requested = request.FreelancerIds.Distinct().ToList();
        var notShortlisted = requested.Where(id => !shortlistedSet.Contains(id)).ToList();

        if (requested.Count > AiWorkflowService.MaxShortlistApprovals)
            return BadRequest(new
            {
                error = $"At most {AiWorkflowService.MaxShortlistApprovals} freelancers may be invited per call; {requested.Count} were supplied.",
            });

        var invitationIds = new List<int>(requested.Count);
        var skipped = new List<object>(requested.Count);

        foreach (var freelancerId in requested)
        {
            if (!shortlistedSet.Contains(freelancerId))
            {
                skipped.Add(new
                {
                    freelancer_id = freelancerId,
                    reason = "not in this workflow's shortlist",
                });
                continue;
            }

            try
            {
                // The package, because an invitation is priced by it and carries
                // no price of its own. Without one, `CreateInvitationAsync`
                // rejects the request with "Invalid package ID", so every
                // freelancer the agent tried to contact would be skipped and the
                // approval would appear to succeed having contacted nobody.
                //
                // Lowest-priced, because that is the least surprising default
                // when the agent is acting on a recorded approval rather than a
                // live one -- and the approval the client gave names *people*,
                // not packages, so no package choice was ever theirs to honour.
                var packageId = await ResolveDefaultPackageIdAsync(freelancerId, cancellationToken);
                if (packageId is null)
                {
                    skipped.Add(new
                    {
                        freelancer_id = freelancerId,
                        reason = "no package is set up for this freelancer, so there is no price to invite at",
                    });
                    continue;
                }

                var created = await _invitations.CreateInvitationAsync(
                    // The *user* id, because `CreateInvitationAsync` resolves the
                    // client profile from it and owns the ownership check. Note
                    // `project.ClientId` is the profile's key, not a user id.
                    project.Client!.UserId,
                    new WebOfWork.Api.DTOs.Invitations.CreateInvitationRequest
                    {
                        ProjectId = project.Id,
                        FreelancerId = freelancerId,
                        PackageId = packageId.Value,
                    });

                invitationIds.Add(created.Id);
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or UnauthorizedAccessException)
            {
                // Business-rule rejections -- already invited, already proposed,
                // deactivated account. Reported, not thrown.
                skipped.Add(new { freelancer_id = freelancerId, reason = ex.Message });
            }
        }

        // Logged as a tool call so the action is auditable in the same place as
        // every other side effect the agent took, and appears in the run's detail
        // view. `Success` reflects whether anything happened at all, not whether
        // everything did.
        await _workflows.AppendToolCallLogAsync(
            request.WorkflowRunId,
            "send_invitations",
            input: System.Text.Json.JsonSerializer.Serialize(new
            {
                project_id = project.Id,
                requested = requested,
                not_shortlisted = notShortlisted,
            }),
            output: System.Text.Json.JsonSerializer.Serialize(new
            {
                invitation_ids = invitationIds,
                skipped,
            }),
            success: invitationIds.Count > 0,
            durationMs: 0,
            cancellationToken);

        if (invitationIds.Count == 0)
        {
            return Conflict(new
            {
                error = "None of the requested freelancers could be invited.",
                invitation_ids = Array.Empty<int>(),
                skipped,
            });
        }

        return Ok(new { invitation_ids = invitationIds, skipped });
    }
}

/// <param name="WorkflowRunId">The approved run this action belongs to.</param>
/// <param name="FreelancerIds">`FreelancerProfile` ids the client approved.</param>
/// <param name="ProjectId">
/// Optional. Falls back to the run's project, and is rejected if it disagrees --
/// a mismatch means the caller has lost track of which run it is acting for.
/// </param>
public sealed class SendInvitationsRequest
{
    public int WorkflowRunId { get; set; }
    public List<Guid> FreelancerIds { get; set; } = new();
    public int? ProjectId { get; set; }
}
