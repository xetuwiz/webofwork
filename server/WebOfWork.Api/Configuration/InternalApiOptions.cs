namespace WebOfWork.Api.Configuration;

/// <summary>
/// API key shared between the ASP.NET API and the Python AI service.
/// Validated by InternalApiKeyMiddleware on every /internal/* request.
/// Applied in BOTH directions: AI → API and API → AI.
/// </summary>
public sealed class InternalApiOptions
{
    public const string Section = "InternalApi";

    public string ApiKey { get; init; } = string.Empty;

    public const string HeaderName = "X-Internal-Api-Key";
    public const string InternalRoutePrefix = "/internal";
}
