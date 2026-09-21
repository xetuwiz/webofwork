using System;
using WebOfWork.Api.Models.Enums;

namespace WebOfWork.Api.Models;

public class Contract
{
    public int Id { get; set; }

    public int ProjectId { get; set; }
    public Project Project { get; set; } = null!;

    public Guid ClientId { get; set; }
    public ClientProfile Client { get; set; } = null!;

    public Guid FreelancerId { get; set; }
    public FreelancerProfile Freelancer { get; set; } = null!;

    public Guid? PackageId { get; set; }
    public FreelancerPackage? Package { get; set; }

    public decimal TotalAmount { get; set; }
    public bool IsFunded { get; set; } = false;
    public bool PaidToFreelancer { get; set; } = false;

    // Phase 9 explicit requests
    public DateTime? PayoutEligibleAfter { get; set; }
    public bool IsMilestoneBased { get; set; } = false;
    public int MilestoneCount { get; set; } = 0;

    public string ClientSignature { get; set; } = string.Empty;
    public string ClientSignatureType { get; set; } = string.Empty;

    public string? FreelancerSignature { get; set; }
    public string? FreelancerSignatureType { get; set; }

    public DateTime ClientSignedAt { get; set; } = DateTime.UtcNow;
    public string? ClientIp { get; set; }

    public DateTime? FreelancerSignedAt { get; set; }
    public string? FreelancerIp { get; set; }

    public string? LegalDocumentUrl { get; set; }
    public DateTime? CompletedAt { get; set; }
    
    public ContractStatus Status { get; set; } = ContractStatus.Offered;
}
