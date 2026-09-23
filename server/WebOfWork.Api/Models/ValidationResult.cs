using System;
using System.Text.Json.Serialization;

namespace WebOfWork.Api.Models;

public class ValidationResult
{
    public int Id { get; set; }

    public int WorkflowRunId { get; set; }
    [JsonIgnore]
    public WorkflowRun WorkflowRun { get; set; } = null!;

    public int ProjectId { get; set; }
    public int ProposalId { get; set; }
    public Guid FreelancerId { get; set; }
    public bool Valid { get; set; }
    public string? IssuesJson { get; set; }
    public string? CostBreakdownJson { get; set; }
    public bool RequiresHumanApproval { get; set; }
    public string? HumanSummary { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
