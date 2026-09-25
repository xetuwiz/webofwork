using System.Collections.Generic;
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

    public DateTime? PayoutEligibleAfter { get; set; }

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

    /// <summary>When the offer/contract record was created. The mobile client
    /// shows this as the offer's "received date".</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ContractStatus Status { get; set; } = ContractStatus.Offered;
    public ICollection<ContractDeliverable> Deliverables { get; set; } = new List<ContractDeliverable>();
    public ICollection<ContractRevision> Revisions { get; set; } = new List<ContractRevision>();
    public Dispute? Dispute { get; set; }
    public Payment? Payment { get; set; }
    public ICollection<Review> Reviews { get; set; } = new List<Review>();
}
