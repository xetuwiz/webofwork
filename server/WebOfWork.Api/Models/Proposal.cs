using System;
using WebOfWork.Api.Models.Enums;

namespace WebOfWork.Api.Models;

public class Proposal
{
    public int Id { get; set; }
    
    public int ProjectId { get; set; }
    public Project Project { get; set; } = null!;

    public Guid FreelancerId { get; set; }
    public FreelancerProfile Freelancer { get; set; } = null!;

    public Guid PackageId { get; set; }
    public FreelancerPackage Package { get; set; } = null!;

    public string CoverLetter { get; set; } = string.Empty;
    public decimal BidAmount { get; set; }
    public int DeliveryDays { get; set; }

    public ProposalStatus Status { get; set; } = ProposalStatus.Pending;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
