namespace WebOfWork.Api.Configuration;

/// <summary>
/// Settings for the Python AI service (ai/).
/// InternalApiKey is the X-Internal-Api-Key shared secret — used in BOTH directions.
/// CheckpointBackend MUST be "postgres" in production; empty string fails closed.
/// MaxInternalReadsPerWorkflow enforces the AI read budget (plan default: 20).
/// </summary>
public sealed class AgenticAiOptions
{
    public const string Section = "AgenticAi";

    public string BaseUrl                      { get; init; } = "http://localhost:8001";
    public string InternalApiKey               { get; init; } = string.Empty;
    public int    MaxInternalReadsPerWorkflow   { get; init; } = 20;
    /// <summary>Must be "postgres" in prod. Empty disables persistence (dev only).</summary>
    public string CheckpointBackend            { get; init; } = string.Empty;
}
