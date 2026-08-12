namespace WebOfWork.Api.Configuration;

/// <summary>
/// LLM provider chain configuration.
/// Provider order: Gemini → Groq → OpenRouter → (Ollama, local dev only).
/// Ollama must never be enabled in production (EnableOllama = false).
/// MaxCallsPerWorkflow and MaxCallsPerDay enforce the AI call budget.
/// </summary>
public sealed class LlmOptions
{
    public const string Section = "Llm";

    /// <summary>Comma-separated provider chain, e.g. "gemini,groq,openrouter".</summary>
    public string   ProviderChain        { get; init; } = "gemini,groq,openrouter";
    /// <summary>Ollama allowed only in local dev. Never in prod.</summary>
    public bool     EnableOllama         { get; init; } = false;
    public string[] GeminiApiKeys        { get; init; } = [];
    public string[] GroqApiKeys          { get; init; } = [];
    public string[] OpenRouterApiKeys    { get; init; } = [];
    public string   OllamaBaseUrl        { get; init; } = "http://host.docker.internal:11434";
    public int      MaxCallsPerWorkflow  { get; init; } = 15;
    public int      MaxCallsPerDay       { get; init; } = 1000;
    public bool     CacheEnabled         { get; init; } = true;
}
