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
    public bool IsMilestoneBased { get; set; }
    public int MilestoneCount { get; set; }
    
    // Signatures (often excluded or minimal in lists, but included here)
    public bool ClientHasSigned { get; set; }
    public DateTime ClientSignedAt { get; set; }
    public bool FreelancerHasSigned { get; set; }
    public DateTime? FreelancerSignedAt { get; set; }
    
    public string? LegalDocumentUrl { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string Status { get; set; } = string.Empty;
}

public class AcceptContractRequest
{
    public string Signature { get; set; } = string.Empty;
    public string SignatureType { get; set; } = string.Empty; // e.g., "type" or "draw"
}

public class UpdateContractStatusRequest
{
    public string Status { get; set; } = string.Empty;
}
