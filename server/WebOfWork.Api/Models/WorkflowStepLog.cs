using System;

namespace WebOfWork.Api.Models;

public class WorkflowStepLog
{
    public int Id { get; set; }

    public int WorkflowRunId { get; set; }
    public WorkflowRun WorkflowRun { get; set; } = null!;

    public string StepName { get; set; } = string.Empty;
    public string? AgentName { get; set; }
    public string? Input { get; set; }
    public string? Output { get; set; }
    public string Status { get; set; } = "Pending";
    public long DurationMs { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
