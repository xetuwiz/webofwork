using System;
using System.Collections.Generic;
using WebOfWork.Api.Models.Enums;

namespace WebOfWork.Api.DTOs.Briefs;

public class CreateBriefRequest
{
    public string Text { get; set; } = string.Empty;
    public decimal? BudgetHint { get; set; }
    public DateTime? DeadlineHint { get; set; }
}

public class BriefResponse
{
    public int Id { get; set; }
    public string Text { get; set; } = string.Empty;
    public decimal? BudgetHint { get; set; }
    public DateTime? DeadlineHint { get; set; }
    public BriefStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    
    public List<BriefAttachmentResponse> Attachments { get; set; } = new();
    public BriefAnalysisResponse? Analysis { get; set; }
}

public class BriefAttachmentResponse
{
    public int Id { get; set; }
    public string FileUrl { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
}

public class BriefAnalysisResponse
{
    public List<string> RequiredSkills { get; set; } = new();
    public string Complexity { get; set; } = string.Empty;
    public decimal SuggestedBudget { get; set; }
    public string Timeline { get; set; } = string.Empty;
    public decimal Confidence { get; set; }
}
