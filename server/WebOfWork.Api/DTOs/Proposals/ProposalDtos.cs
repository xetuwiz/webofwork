using System;

namespace WebOfWork.Api.DTOs.Proposals;

public class CreateProposalRequest
{
    public int ProjectId { get; set; }
    public Guid PackageId { get; set; }
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
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
