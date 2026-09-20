using System;
using System.Collections.Generic;

namespace WebOfWork.Api.Models;

public class BriefAnalysis
{
    public int Id { get; set; }
    public int BriefId { get; set; }
    public Brief Brief { get; set; } = null!;
    
    public List<string> RequiredSkills { get; set; } = new();
    public string Complexity { get; set; } = string.Empty;
    public decimal SuggestedBudget { get; set; }
    public string Timeline { get; set; } = string.Empty;
    public decimal Confidence { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
