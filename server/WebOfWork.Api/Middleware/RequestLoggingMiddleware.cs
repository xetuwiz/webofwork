using System.Diagnostics;
using Serilog.Context;

namespace WebOfWork.Api.Middleware;

/// <summary>
/// Structured request/response logging middleware.
/// Pushes CorrelationId into Serilog's LogContext so all log entries within the
/// request carry the correlation ID (confirmed requirement in manual.md Phase 1).
/// Logs method, path, status code, and elapsed time.
/// </summary>
public sealed class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;

    public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
    {
        _next   = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Items.TryGetValue("CorrelationId", out var id)
            ? id?.ToString()
            : context.TraceIdentifier;

        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            var sw = Stopwatch.StartNew();

            _logger.LogInformation(
                "HTTP {Method} {Path} started",
                context.Request.Method,
                context.Request.Path);

            await _next(context);

            sw.Stop();

            _logger.LogInformation(
                "HTTP {Method} {Path} responded {StatusCode} in {ElapsedMs}ms",
                context.Request.Method,
                context.Request.Path,
                context.Response.StatusCode,
                sw.ElapsedMilliseconds);
        }
    }
}

public static class RequestLoggingMiddlewareExtensions
{
    public static IApplicationBuilder UseRequestLoggingMiddleware(this IApplicationBuilder app)
        => app.UseMiddleware<RequestLoggingMiddleware>();
}
