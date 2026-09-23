using System;
using System.Text.Json.Serialization;

namespace WebOfWork.Api.Models;

public class ToolCallLog
{
    public int Id { get; set; }

    public int WorkflowRunId { get; set; }
    [JsonIgnore]
    public WorkflowRun WorkflowRun { get; set; } = null!;

    public string ToolName { get; set; } = string.Empty;
    public string? Input { get; set; }
    public string? Output { get; set; }
    public bool Success { get; set; }
    public long DurationMs { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
