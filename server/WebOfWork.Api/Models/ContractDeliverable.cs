using System;

namespace WebOfWork.Api.Models;

public class ContractDeliverable
{
    public int Id { get; set; }
    
    public int ContractId { get; set; }
    public Contract Contract { get; set; } = null!;

    public Guid FreelancerId { get; set; }
    public FreelancerProfile Freelancer { get; set; } = null!;

    public string Title { get; set; } = string.Empty;
    public string? FileUrl { get; set; }
    public string? LinkUrl { get; set; }
    public string? Notes { get; set; }
    
    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;
}
