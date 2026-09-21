using System;

namespace WebOfWork.Api.Models;

public class MilestoneDeliverable
{
    public int Id { get; set; }
    
    public int MilestoneId { get; set; }
    public Milestone Milestone { get; set; } = null!;

    public string? FileUrl { get; set; }
    public string Notes { get; set; } = string.Empty;

    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;
}
