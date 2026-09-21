using System;
using System.Collections.Generic;

namespace WebOfWork.Api.DTOs.Milestones;

public class MilestoneResponse
{
    public int Id { get; set; }
    public int ContractId { get; set; }
    public int Order { get; set; }
    public string Title { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public int ExpectedDays { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? FundedAt { get; set; }
    
    public List<MilestoneDeliverableResponse> Deliverables { get; set; } = new();
}

public class MilestoneDeliverableResponse
{
    public int Id { get; set; }
    public string? FileUrl { get; set; }
    public string Notes { get; set; } = string.Empty;
    public DateTime SubmittedAt { get; set; }
}

public class CreateMilestoneRequest
{
    public string Title { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public int ExpectedDays { get; set; }
}

public class SubmitMilestoneRequest
{
    public string? FileUrl { get; set; }
    public string Notes { get; set; } = string.Empty;
}
