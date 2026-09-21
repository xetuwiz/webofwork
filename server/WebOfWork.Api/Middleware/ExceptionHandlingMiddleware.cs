using Microsoft.AspNetCore.Mvc;
using System.Net;
using System.Text.Json;

namespace WebOfWork.Api.Middleware;

/// <summary>
/// Global exception handler middleware.
/// Catches all unhandled exceptions and returns RFC 7807 ProblemDetails responses.
/// Must be the FIRST middleware in the pipeline to catch exceptions from all subsequent layers.
/// </summary>
public sealed class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next   = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception ex)
    {
        var correlationId = context.Items.TryGetValue("CorrelationId", out var id)
            ? id?.ToString()
            : context.TraceIdentifier;

        _logger.LogError(ex, "Unhandled exception. CorrelationId: {CorrelationId} Path: {Path}",
            correlationId, context.Request.Path);

        var (statusCode, title) = ex switch
        {
            ArgumentException              => (HttpStatusCode.BadRequest, "Bad Request"),
            InvalidOperationException      => (HttpStatusCode.BadRequest, "Bad Request"),
            UnauthorizedAccessException    => (HttpStatusCode.Unauthorized, "Unauthorized"),
            KeyNotFoundException           => (HttpStatusCode.NotFound, "Not Found"),
            OperationCanceledException     => (HttpStatusCode.ServiceUnavailable, "Request Cancelled"),
            _                              => (HttpStatusCode.InternalServerError, "Internal Server Error")
        };

        var problem = new ProblemDetails
        {
            Status   = (int)statusCode,
            Title    = title,
            Detail   = ex.Message,
            Instance = context.Request.Path,
            Extensions =
            {
                ["correlationId"] = correlationId,
                ["traceId"]       = context.TraceIdentifier
            }
        };

        context.Response.ContentType = "application/problem+json";
        context.Response.StatusCode  = (int)statusCode;

        await context.Response.WriteAsync(
            JsonSerializer.Serialize(problem, _jsonOptions));
    }
}

public static class ExceptionHandlingMiddlewareExtensions
{
    public static IApplicationBuilder UseExceptionHandlingMiddleware(this IApplicationBuilder app)
        => app.UseMiddleware<ExceptionHandlingMiddleware>();
}
