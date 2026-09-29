using Microsoft.EntityFrameworkCore;
using WebOfWork.Api.Data;
using WebOfWork.Api.Models;

namespace WebOfWork.Api.Services;

/// <summary>
/// Decides which agent stage a project is ready for.
/// </summary>
/// <remarks>
/// The agent graph is a pipeline, and a project joins it at different points
/// depending on what already exists. A project that has just been posted needs
/// talent matching. One that already has proposals needs screening. One that has
/// been through screening needs validation. Re-deriving that from request-shaped
/// input at each call site is how a project gets screened twice, or matched after
/// a shortlist already exists.
/// </remarks>
public interface IStageResolver
{
    /// <summary>
    /// The stage to run next for <paramref name="projectId"/>.
    /// </summary>
    Task<WorkflowStageResolution> ResolveForProjectAsync(int projectId, CancellationToken ct = default);
}

/// <summary>
/// What the resolver found, and why.
/// </summary>
/// <remarks>
/// The evidence is returned rather than only the answer. When a run lands in the
/// wrong stage the useful question is not "what did you decide" but "what did you
/// see", and a bare string forces the investigation to be repeated from scratch.
/// </remarks>
public sealed record WorkflowStageResolution(
    string Stage,
    bool HasProposals,
    bool HasShortlist,
    bool HasValidation,
    int ProposalCount,
    int ShortlistCount)
{
    /// <summary>One sentence, for a log line or an admin-facing reason.</summary>
    public string Rationale => Stage switch
    {
        WorkflowStages.TalentMatch => HasProposals
            ? "Proposals exist but the project has never been screened, so it resumes at screening."
            : "No proposals and no shortlist yet, so the project starts at talent matching.",
        WorkflowStages.Screen => $"{ProposalCount} proposal(s) present and no validation recorded.",
        WorkflowStages.Validate => $"A shortlist of {ShortlistCount} exists and validation has already run.",
        _ => $"No rule matched; defaulting to {Stage}.",
    };
}

public class StageResolver : IStageResolver
{
    private readonly WebOfWorkDbContext _db;

    public StageResolver(WebOfWorkDbContext db) => _db = db;

    /// <summary>
    /// The decision, as a pure function.
    /// </summary>
    /// <remarks>
    /// Kept separate from the database query so the precedence rules can be tested
    /// directly, with no fixture and no container. The rules are short but the
    /// ordering is the part that matters, and ordering is exactly what a
    /// database-backed test is worst at pinning down.
    /// </remarks>
    public static WorkflowStageResolution Resolve(
        bool hasProposals,
        bool hasShortlist,
        bool hasValidation,
        int proposalCount = 0,
        int shortlistCount = 0)
    {
        // Precedence, most-specific first:
        //
        //   1. A validation result means the pipeline already reached the gate.
        //      Re-running it would duplicate the approval, so validate.
        //   2. Proposals with no validation means matching already happened and
        //      people applied, so screen.
        //   3. Anything else -- including a shortlist that has not been validated
        //      -- is a candidate for talent matching.
        //
        // Note that (3) puts a shortlist back into talent_match. That is
        // deliberate: matching is what produces a shortlist, so a project holding
        // one without a validation result is mid-pipeline, and the client-approval
        // gate is where it is supposed to stop.
        if (hasValidation)
            return new WorkflowStageResolution(WorkflowStages.Validate, hasProposals, hasShortlist, hasValidation, proposalCount, shortlistCount);

        if (hasProposals)
            return new WorkflowStageResolution(WorkflowStages.Screen, hasProposals, hasShortlist, hasValidation, proposalCount, shortlistCount);

        return new WorkflowStageResolution(WorkflowStages.TalentMatch, hasProposals, hasShortlist, hasValidation, proposalCount, shortlistCount);
    }

    public async Task<WorkflowStageResolution> ResolveForProjectAsync(int projectId, CancellationToken ct = default)
    {
        // Each fact is asked independently, because they are produced by
        // different agents and a project can have some without others. Short-
        // circuiting on the first hit would mean asking for less than the decision
        // needs -- and the returned counts are the evidence a caller inspects when
        // the answer looks wrong.
        var proposalCount = await _db.Proposals
            .AsNoTracking()
            .CountAsync(p => p.ProjectId == projectId, ct);

        var shortlistCount = await _db.AgentRecommendations
            .AsNoTracking()
            .CountAsync(r => r.ProjectId == projectId, ct);

        var hasValidation = await _db.ValidationResults
            .AsNoTracking()
            .AnyAsync(v => v.ProjectId == projectId, ct);

        return Resolve(
            hasProposals: proposalCount > 0,
            hasShortlist: shortlistCount > 0,
            hasValidation: hasValidation,
            proposalCount: proposalCount,
            shortlistCount: shortlistCount);
    }
}
