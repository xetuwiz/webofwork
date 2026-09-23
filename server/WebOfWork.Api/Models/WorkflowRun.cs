using System;

namespace WebOfWork.Api.Models;

public enum WorkflowRunStatus
{
    Pending = 0,
    Running = 1,
    PendingApproval = 2,
    ValidationFailed = 3,
    Approved = 4,
    Rejected = 5,
    Completed = 6,
    Failed = 7,
    Cancelled = 8
}

public class WorkflowRun
{
    public int Id { get; set; }

    public string Objective { get; set; } = string.Empty;
    public string? Plan { get; set; }
    public WorkflowRunStatus Status { get; set; } = WorkflowRunStatus.Pending;

    public int? ProjectId { get; set; }
    public Project? Project { get; set; }

    public Guid RequestedByUserId { get; set; }
    public User RequestedByUser { get; set; } = null!;

    public string? ApprovalDecision { get; set; }
    public Guid? ApprovedByUserId { get; set; }
    public User? ApprovedByUser { get; set; }
    public DateTime? ApprovedAt { get; set; }

    public string? FinalOutput { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
