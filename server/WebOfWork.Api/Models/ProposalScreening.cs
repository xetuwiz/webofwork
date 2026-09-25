using System;
using System.Text.Json.Serialization;

namespace WebOfWork.Api.Models;

public class ProposalScreening
{
    public int Id { get; set; }

    public int WorkflowRunId { get; set; }
    [JsonIgnore]
    public WorkflowRun WorkflowRun { get; set; } = null!;

    public int ProposalId { get; set; }
    public decimal FitScore { get; set; }
    public string? StrengthsJson { get; set; }
    public string? ConcernsJson { get; set; }
    public string Recommendation { get; set; } = string.Empty;
    public string? Rationale { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
