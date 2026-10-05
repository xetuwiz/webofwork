using System;
using System.Text.Json.Serialization;

namespace WebOfWork.Api.Models;

public class WorkflowStepLog
{
    public int Id { get; set; }

    public int WorkflowRunId { get; set; }

    /// <summary>
    /// The owning run. Needed for the EF relationship, never sent to a client.
    /// </summary>
    /// <remarks>
    /// This is a back-reference, and it closes a cycle:
    /// <c>WorkflowRun.StepLogs[n].WorkflowRun.StepLogs[n].WorkflowRun...</c>
    /// The serializer walks it until it hits the default depth limit and throws
    /// <c>JsonException: A possible object cycle was detected</c>, so the response
    /// is truncated and the client sees a failed load rather than a run.
    ///
    /// It hid for a long time because an earlier authorization failure on this
    /// endpoint returned 403 *before* serialization ran, so nothing ever reached
    /// the serializer. Fixing the 403 exposed this immediately -- the two bugs
    /// were stacked, and the outer one was hiding the inner one.
    ///
    /// <c>ReferenceHandler.Preserve</c> would make it serialize, at the cost of
    /// emitting <c>$id</c>/<c>$ref</c> markers and a payload several times larger
    /// for a graph the client has no use for. <c>WorkflowRunId</c> above is the
    /// value a client actually needs to correlate the two.
    /// </remarks>
    [JsonIgnore]
    public WorkflowRun WorkflowRun { get; set; } = null!;

    public string StepName { get; set; } = string.Empty;
    public string? AgentName { get; set; }
    public string? Input { get; set; }
    public string? Output { get; set; }
    public string Status { get; set; } = "Pending";
    public long DurationMs { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
