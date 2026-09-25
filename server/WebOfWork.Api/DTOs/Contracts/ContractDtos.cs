using System;

namespace WebOfWork.Api.DTOs.Contracts;

public class ContractResponse
{
    public int Id { get; set; }
    public int ProjectId { get; set; }
    public string ProjectTitle { get; set; } = string.Empty;
    public Guid ClientId { get; set; }
    public string ClientName { get; set; } = string.Empty;
    public Guid FreelancerId { get; set; }
    public string FreelancerName { get; set; } = string.Empty;
    public Guid? PackageId { get; set; }
    public decimal TotalAmount { get; set; }
    public bool IsFunded { get; set; }
    public bool PaidToFreelancer { get; set; }
    public DateTime? PayoutEligibleAfter { get; set; }

    // Signatures (often excluded or minimal in lists, but included here)
    public bool ClientHasSigned { get; set; }
    public DateTime ClientSignedAt { get; set; }
    public bool FreelancerHasSigned { get; set; }
    public DateTime? FreelancerSignedAt { get; set; }

    public string? LegalDocumentUrl { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public WebOfWork.Api.Models.Enums.ContractStatus Status { get; set; }

    // ── Read by Contracts.jsx / ClientContracts.jsx / ContractDetail.jsx ──
    public string PackageName { get; set; } = string.Empty;
    public int DeliveryDays { get; set; }
    public string ProjectDescription { get; set; } = string.Empty;
    /// <summary>What the freelancer actually earns on this contract, net of platform fee.</summary>
    public decimal FreelancerEarnings { get; set; }
    public List<WebOfWork.Api.DTOs.Deliverables.DeliverableResponse> Deliverables { get; set; } = new();
    public List<WebOfWork.Api.DTOs.Revisions.ContractRevisionResponse> Revisions { get; set; } = new();

    /// <summary>
    /// The freelancer's USER id. FreelancerId is the freelancer_profiles row id, but
    /// the chat endpoint matches on users.id, so passing FreelancerId there failed
    /// the EXISTS lookup every time and returned 403.
    /// </summary>
    public Guid? FreelancerUserId { get; set; }

    /// <summary>Client's USER id. ClientId is the client_profiles row id; the chat
    /// endpoint matches on users.id.</summary>
    public Guid? ClientUserId { get; set; }
    public WebOfWork.Api.DTOs.Disputes.DisputeResponse? DisputeDetails { get; set; }
    public string? PaymentStatus { get; set; }
    // ContractDetail.jsx reads `reviews.client_review` and
    // `reviews.freelancer_review` as a nested object. This was previously a
    // List<object> of milestones-shaped rows, so both detail screens always
    // rendered "no review yet" even after a review had been submitted.
    public ContractReviewsResponse Reviews { get; set; } = new();
    // Read by both ContractDetail.jsx files but never declared, so the UI fell back to
    // "No specific requirements provided." / "No specific deliverables stated." even
    // though the project has both. They live on the project, not the contract.
    public string? Requirements { get; set; }
    public string? ExpectedDeliverables { get; set; }
    // Both ContractDetail.jsx files render contract.skills for "Required Technologies",
    // so that is the name that must be populated. RequiredTechnologies is kept as an
    // alias because the two detail pages are not the only consumers.
    public List<string> Skills { get; set; } = new();
    public List<string> RequiredTechnologies { get; set; } = new();
}

/// <summary>
/// The two reviews a contract can accumulate: the client's review of the
/// freelancer (`client_review`, `ReviewType.Freelancer`) and the freelancer's
/// review of the client (`freelancer_review`, `ReviewType.Client`).
/// </summary>
public class ContractReviewsResponse
{
    public WebOfWork.Api.DTOs.Reviews.ReviewResponse? ClientReview { get; set; }
    public WebOfWork.Api.DTOs.Reviews.ReviewResponse? FreelancerReview { get; set; }
}

public class AcceptContractRequest
{
    public string Signature { get; set; } = string.Empty;
    public string SignatureType { get; set; } = string.Empty; // e.g., "type" or "draw"
}

public class UpdateContractStatusRequest
{
    public WebOfWork.Api.Models.Enums.ContractStatus Status { get; set; }
    public string? PaymentStatus { get; set; }
}
