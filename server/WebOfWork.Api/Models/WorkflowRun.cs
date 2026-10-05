using System;

namespace WebOfWork.Api.Models;

public enum WorkflowRunStatus
{
    Pending = 0,
    Running = 1,

    /// <summary>
    /// Awaiting a human decision, without saying which human.
    /// </summary>
    /// <remarks>
    /// Kept at its original value of 2 rather than renumbered, because
    /// <c>workflow_runs.status</c> is persisted as an integer and a deployment
    /// may already hold rows. It remains the fallback for a stage with no more
    /// specific gate; new code should prefer
    /// <see cref="PendingClientApproval"/> or <see cref="PendingManagerApproval"/>,
    /// which a client can act on.
    /// </remarks>
    PendingApproval = 2,

    ValidationFailed = 3,
    Approved = 4,
    Rejected = 5,
    Completed = 6,
    Failed = 7,
    Cancelled = 8,

    /// <summary>
    /// Paused waiting for the client who owns the project -- to approve a
    /// shortlist, typically.
    /// </summary>
    /// <remarks>
    /// A distinct state from <see cref="PendingManagerApproval"/> because the two
    /// gates have different owners, different UI, and different resume paths.
    /// A client polling "is my workflow waiting on me?" cannot tell the answer if
    /// both collapse into one value.
    ///
    /// Numbered 9 rather than slotting into the sequence so no existing stored
    /// value changes meaning.
    /// </remarks>
    PendingClientApproval = 9,

    /// <summary>
    /// Paused waiting for a manager or admin -- to approve a contract, typically.
    /// </summary>
    /// <remarks>See <see cref="PendingClientApproval"/> for why the values are not contiguous.</remarks>
    PendingManagerApproval = 10,
}

/// <summary>
/// The stage vocabulary, shared with the Python agent service.
/// </summary>
/// <remarks>
/// <para>
/// Stored as a <c>string</c> rather than an enum on purpose. The Python service
/// owns this vocabulary -- it dispatches on it and sends it back on every
/// <c>agent/chat</c> call -- so an int enum here would need a second mapping table
/// in C# that could drift from the one in Python. Storing the wire value verbatim
/// makes the two impossible to disagree silently.
/// </para>
/// <para>
/// The cost is that a typo is storable, which is why <see cref="IsKnown"/> exists
/// and <see cref="Normalise"/> is used at every write site rather than assigning
/// the raw string.
/// </para>
/// </remarks>
public static class WorkflowStages
{
    /// <summary>Turn a client's free-text brief into a structured draft.</summary>
    public const string Draft = "draft";

    /// <summary>Plan the work, score the freelancer pool, and shortlist candidates.</summary>
    public const string TalentMatch = "talent_match";

    /// <summary>Rank the proposals that came in against a posted project.</summary>
    public const string Screen = "screen";

    /// <summary>The deterministic safety gate. Runs on its own or at the end of a shortlist.</summary>
    public const string Validate = "validate";

    /// <summary>Send invitations to the shortlist the client approved.</summary>
    public const string Outreach = "outreach";

    private static readonly HashSet<string> Known = new(StringComparer.OrdinalIgnoreCase)
    {
        Draft, TalentMatch, Screen, Validate, Outreach
    };

    /// <summary>
    /// The vocabulary, in pipeline order.
    /// </summary>
    /// <remarks>
    /// Exposed so the database check constraint on <c>workflow_runs.stage</c> is
    /// generated from this list rather than restated as literals. A constraint
    /// written out by hand is a second copy of the vocabulary, and it is the copy
    /// that would be forgotten when a stage is added.
    /// </remarks>
    public static IReadOnlyList<string> All { get; } = new[] { Draft, TalentMatch, Screen, Validate, Outreach };

    public static bool IsKnown(string? stage) => stage is not null && Known.Contains(stage);

    /// <summary>
    /// Trims and lower-cases <paramref name="stage"/>, rejecting anything not in
    /// the vocabulary.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// The stage is unknown. Thrown rather than defaulted, because a run recorded
    /// under the wrong stage would dispatch to the wrong agent graph and fail
    /// later, far from the cause.
    /// </exception>
    public static string Normalise(string? stage)
    {
        if (string.IsNullOrWhiteSpace(stage))
            throw new ArgumentException("A workflow stage is required.", nameof(stage));

        var trimmed = stage.Trim().ToLowerInvariant();
        if (!Known.Contains(trimmed))
            throw new ArgumentException(
                $"Unknown workflow stage '{stage}'. Expected one of: {string.Join(", ", Known)}.",
                nameof(stage));

        return trimmed;
    }

    /// <summary>
    /// The gate a stage pauses on, or null when it runs to completion without
    /// asking a human.
    /// </summary>
    /// <remarks>
    /// Centralised so "which human waits here?" is answered in one place instead
    /// of by a chain of comparisons at each call site -- which is how the two
    /// gates end up sharing a status value.
    /// </remarks>
    public static WorkflowRunStatus? ApprovalGateFor(string stage) => stage switch
    {
        // The client owns the project and picks who to approach.
        TalentMatch => WorkflowRunStatus.PendingClientApproval,
        // An admin signs off before money moves.
        Validate => WorkflowRunStatus.PendingManagerApproval,
        _ => null,
    };
}

public class WorkflowRun
{
    public int Id { get; set; }

    public string Objective { get; set; } = string.Empty;

    public string? Plan { get; set; }

    public WorkflowRunStatus Status { get; set; } = WorkflowRunStatus.Pending;

    /// <summary>
    /// Which agent stage this run is in. See <see cref="WorkflowStages"/>.
    /// </summary>
    public string Stage { get; set; } = WorkflowStages.Draft;

    public int? ProjectId { get; set; }
    public Project? Project { get; set; }

    public Guid RequestedByUserId { get; set; }
    public User RequestedByUser { get; set; } = null!;

    public string? ApprovalDecision { get; set; }
    public string? ApprovalRationale { get; set; }
    public Guid? ApprovedByUserId { get; set; }
    public User? ApprovedByUser { get; set; }
    public DateTime? ApprovedAt { get; set; }

    public string? FinalOutput { get; set; }

    /// <summary>
    /// What the human supplied when they approved, as jsonb -- for a shortlist
    /// approval, the freelancer ids they actually chose.
    /// </summary>
    /// <remarks>
    /// Separate from <see cref="Plan"/> because they are written by different
    /// parties and have different trust levels. <c>Plan</c> is agent output and
    /// arrives from the Python service; this is a human decision recorded verbatim
    /// and replayed to resume the graph. Keeping them apart is what stops a
    /// resumed run from re-deriving, and possibly silently altering, a choice a
    /// person already made.
    /// </remarks>
    public string? ResumeValue { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public ICollection<WorkflowStepLog> StepLogs { get; set; } = new List<WorkflowStepLog>();
}
