using System;
using System.Collections.Generic;
using WebOfWork.Api.Models.Enums;

namespace WebOfWork.Api.Models;

public class Brief
{
    public int Id { get; set; }
    public Guid ClientId { get; set; }
    public ClientProfile Client { get; set; } = null!;
    
    public string Text { get; set; } = string.Empty;
    public decimal? BudgetHint { get; set; }
    public DateTime? DeadlineHint { get; set; }
    
    public BriefStatus Status { get; set; } = BriefStatus.Pending;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    public ICollection<BriefAttachment> Attachments { get; set; } = new List<BriefAttachment>();
    public BriefAnalysis? Analysis { get; set; }
}
