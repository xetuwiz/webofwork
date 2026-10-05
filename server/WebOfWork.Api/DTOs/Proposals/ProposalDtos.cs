using System;

namespace WebOfWork.Api.DTOs.Proposals;

public class CreateProposalRequest
{
    public int ProjectId { get; set; }
    public int Project { get; set; }
    public Guid PackageId { get; set; }
    public Guid Package { get; set; }
    public string CoverLetter { get; set; } = string.Empty;
    public decimal BidAmount { get; set; }
    public int DeliveryDays { get; set; }
}

public class ProposalResponse
{
    public int Id { get; set; }
    public int ProjectId { get; set; }
    public string ProjectTitle { get; set; } = string.Empty;
    public Guid FreelancerId { get; set; }
    public string FreelancerName { get; set; } = string.Empty;
    public Guid PackageId { get; set; }
    public string CoverLetter { get; set; } = string.Empty;
    public decimal BidAmount { get; set; }
    public int DeliveryDays { get; set; }
    public WebOfWork.Api.Models.Enums.ProposalStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }

    // ── Read by ProposalsPage.jsx (the reference ProposalsListSerializer) ──
    public string FreelancerPhoto { get; set; } = string.Empty;
    public string FreelancerTagline { get; set; } = string.Empty;
    public decimal FreelancerRatings { get; set; }
    /// <summary>Display name of the client who posted the project, for the
    /// freelancer's own "My Proposals" list. Requires the Project -> Client ->
    /// User include chain.</summary>
    public string ClientName { get; set; } = string.Empty;
    /// <summary>Contract for this project, or null when none exists yet.</summary>
    public ProposalContractInfo? ContractInfo { get; set; }
}

public class ProposalContractInfo
{
    public int Id { get; set; }
    /// <summary>snake_case, matching the status badges the cards compare against.</summary>
    public string Status { get; set; } = string.Empty;
    public bool IsThisFreelancer { get; set; }
}
