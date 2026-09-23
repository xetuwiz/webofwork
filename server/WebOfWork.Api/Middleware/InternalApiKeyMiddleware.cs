using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Text;
using WebOfWork.Api.Configuration;
using System.Text.Json;

namespace WebOfWork.Api.Middleware;

/// <summary>
/// Internal API key middleware.
/// Any request to /internal/* MUST carry the X-Internal-Api-Key header matching InternalApi:ApiKey.
/// Must be registered BEFORE UseRouting() — this is locked in the middleware pipeline spec.
/// Used to authenticate calls from the Python AI service to /internal/read/* endpoints.
/// AI tools NEVER call /api/* directly.
/// </summary>
public sealed class InternalApiKeyMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IOptions<InternalApiOptions> _options;
    private readonly ILogger<InternalApiKeyMiddleware> _logger;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public InternalApiKeyMiddleware(
        RequestDelegate next,
        IOptions<InternalApiOptions> options,
        ILogger<InternalApiKeyMiddleware> logger)
    {
        _next = next;
        _options = options;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? string.Empty;

        var isInternalRoute = path.Equals(InternalApiOptions.InternalRoutePrefix, StringComparison.OrdinalIgnoreCase)
            || path.StartsWith(InternalApiOptions.InternalRoutePrefix + "/", StringComparison.OrdinalIgnoreCase);

        if (isInternalRoute)
        {
            var incomingKey = context.Request.Headers[InternalApiOptions.HeaderName].FirstOrDefault();
            var expectedKey = _options.Value.ApiKey;

            var keysMatch = !string.IsNullOrWhiteSpace(expectedKey)
                && !string.IsNullOrWhiteSpace(incomingKey)
                && CryptographicOperations.FixedTimeEquals(
                    Encoding.UTF8.GetBytes(incomingKey),
                    Encoding.UTF8.GetBytes(expectedKey));

            if (!keysMatch)
            {
                var correlationId = context.Items.TryGetValue("CorrelationId", out var id)
                    ? id?.ToString()
                    : context.TraceIdentifier;

                _logger.LogWarning(
                    "Unauthorized internal API access. Path: {Path} CorrelationId: {CorrelationId}",
                    path, correlationId);

                context.Response.ContentType = "application/problem+json";
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;

                var problem = new ProblemDetails
                {
                    Status = StatusCodes.Status401Unauthorized,
                    Title = "Unauthorized",
                    Detail = "Missing or invalid X-Internal-Api-Key.",
                    Instance = context.Request.Path,
                    Extensions = { ["correlationId"] = correlationId }
                };

                await context.Response.WriteAsync(
                    JsonSerializer.Serialize(problem, _jsonOptions));

                return;
            }
        }

        await _next(context);
    }
}

public static class InternalApiKeyMiddlewareExtensions
{
    public static IApplicationBuilder UseInternalApiKeyMiddleware(this IApplicationBuilder app)
        => app.UseMiddleware<InternalApiKeyMiddleware>();
}
