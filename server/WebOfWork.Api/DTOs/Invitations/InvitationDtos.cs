using System;
using WebOfWork.Api.Models.Enums;

namespace WebOfWork.Api.DTOs.Invitations;

public class CreateInvitationRequest
{
    public Guid Freelancer { get; set; }
    public Guid FreelancerId { get; set; }
    public int Project { get; set; }
    public int ProjectId { get; set; }
    public Guid Package { get; set; }
    public Guid PackageId { get; set; }
    public string? Message { get; set; }
}
public class UpdateInvitationStatusRequest { public InvitationStatus Status { get; set; } }
public class InvitationResponse
{
    public int Id { get; set; } public Guid Client { get; set; } public Guid ClientId { get; set; } public string ClientName { get; set; } = string.Empty;
    public Guid Freelancer { get; set; } public Guid FreelancerId { get; set; } public string FreelancerName { get; set; } = string.Empty; public string? FreelancerImage { get; set; } public string FreelancerTagline { get; set; } = string.Empty;
    public int Project { get; set; } public int ProjectId { get; set; } public string ProjectTitle { get; set; } = string.Empty; public Guid Package { get; set; } public Guid PackageId { get; set; } public string? PackageType { get; set; } public decimal? PackageAmount { get; set; }
    public string? Message { get; set; } public WebOfWork.Api.Models.Enums.InvitationStatus Status { get; set; } public DateTime CreatedAt { get; set; } public object? ContractInfo { get; set; }

    /// <summary>
    /// Why this freelancer was matched, when the invitation came from an
    /// AI-assisted shortlist. Null otherwise.
    /// </summary>
    /// <remarks>
    /// An invitation sent by hand has no reason, and a client who picked
    /// someone from a list they built themselves has not been through a model.
    /// So this is nullable rather than a default "you were chosen" string --
    /// a fabricated reason would be worse than no reason, because a freelancer
    /// would weigh it as though it meant something.
    /// </remarks>
    public MatchContextDto? MatchContext { get; set; }
}

/// <summary>
/// The AI's reasoning for one recommendation, shown to the freelancer it is
/// about.
/// </summary>
public class MatchContextDto
{
    /// <summary>The shortlist score, 0-100. Not a percentage of anything.</summary>
    public decimal Score { get; set; }

    /// <summary>
    /// One sentence, written for the freelancer. Produced by the matching agent
    /// and stored verbatim.
    /// </summary>
    public string? Reason { get; set; }

    /// <summary>
    /// The named factors behind the score, e.g. "React", "4.8 rating".
    /// </summary>
    /// <remarks>
    /// Parsed from the stored JSON and flattened to strings. If that JSON is
    /// unreadable the list comes back empty rather than the request failing:
    /// a malformed factor blob is not a reason to hide the whole invitation.
    /// </remarks>
    public List<string> Strengths { get; set; } = new();

    /// <summary>Skills the project wanted and this freelancer lacks.</summary>
    public List<string> Gaps { get; set; } = new();
}
