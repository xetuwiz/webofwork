namespace WebOfWork.Api.Middleware;

/// <summary>
/// Correlation ID middleware.
/// Reads X-Correlation-Id from the inbound request; generates a new GUID if absent.
/// Stores in HttpContext.Items["CorrelationId"] for use by downstream middleware and handlers.
/// Echoes the ID back in the response header for client-side tracing.
/// </summary>
public sealed class CorrelationIdMiddleware
{
    public const string HeaderName = "X-Correlation-Id";

    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers[HeaderName].FirstOrDefault()
                            ?? Guid.NewGuid().ToString("N");

        context.Items["CorrelationId"] = correlationId;
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = correlationId;
            return Task.CompletedTask;
        });

        await _next(context);
    }
}

public static class CorrelationIdMiddlewareExtensions
{
    public static IApplicationBuilder UseCorrelationIdMiddleware(this IApplicationBuilder app)
        => app.UseMiddleware<CorrelationIdMiddleware>();
}
