using System;
using System.Text.Json.Serialization;

namespace WebOfWork.Api.Models;

public class AgentRecommendation
{
    public int Id { get; set; }

    public int WorkflowRunId { get; set; }
    [JsonIgnore]
    public WorkflowRun WorkflowRun { get; set; } = null!;

    public int ProjectId { get; set; }
    public Guid FreelancerId { get; set; }
    public decimal Score { get; set; }
    public string? FactorsJson { get; set; }
    public string? Reason { get; set; }
    public string? PortfolioSamplesJson { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
