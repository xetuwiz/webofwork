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
    private readonly IHostEnvironment _environment;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// Whether the exception's own text may be returned to the caller.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The distinction that matters is not environment, it is <em>who wrote the
    /// message</em>.
    /// </para>
    /// <para>
    /// The four exception types mapped to 4xx above are thrown deliberately, by us,
    /// with a message written to be read by whoever triggered them -- "Contract is
    /// not completed.", "Payout is not yet eligible.", "You do not own this
    /// project." Withholding those leaves the caller with a bare status code, and
    /// the admin payout screen then shows "Payout failed" with no reason, which is
    /// strictly worse than the leak it avoids.
    /// </para>
    /// <para>
    /// The 500 branch is where the real disclosure risk lives: an unexpected
    /// exception carries EF and Npgsql text including table and column names and
    /// occasionally parameter values. That is never returned in Production.
    /// </para>
    /// <para>
    /// An earlier version of this gated on <c>IsDevelopment()</c> alone. That was
    /// the wrong axis -- it protected nothing that was unsafe and broke every
    /// actionable error message in Production at the same time.
    /// </para>
    /// </remarks>
    private bool CanReturnDetail(Exception ex) =>
        ex is ArgumentException
        or InvalidOperationException
        or UnauthorizedAccessException
        or KeyNotFoundException
        || _environment.IsDevelopment()
        || _environment.IsEnvironment("Testing");

    public ExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<ExceptionHandlingMiddleware> logger,
        IHostEnvironment environment)
    {
        _next        = next;
        _logger      = logger;
        _environment = environment;
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
            // Nulled rather than omitted for exceptions we did not author, so the
            // JSON shape does not change between environments: a client parsing
            // ProblemDetails should not have to handle a missing `detail` in
            // production and a present one in development.
            Detail   = CanReturnDetail(ex) ? ex.Message : null,
            Instance = context.Request.Path,
            Extensions =
            {
                ["correlationId"] = correlationId,
                ["traceId"]       = context.TraceIdentifier
            }
        };

        // Once the response has started there is no status line left to change and
        // no header dictionary left to write to. Setting either throws, and the
        // throw replaces the real exception with a confusing one.
        //
        // A client that navigates away mid-websocket-dispatch produces
        // `WebSocketException: the remote party closed the connection without
        // completing the close handshake` routinely -- it is how a browser says
        // goodbye, not a fault. Observed live turning one disconnect into two
        // exceptions: this one, then
        // `InvalidOperationException: Headers are read-only, response has already
        // started` from the ContentType assignment below, which then tripped the
        // developer exception page as well.
        //
        // So: log it, and stop. There is no response left to shape and nobody
        // waiting to read one -- the peer is already gone. Returning rather than
        // rethrowing is also the only option here, since `throw;` is not legal
        // outside a catch block and rethrowing from a helper would discard the
        // original stack unless carried through `ExceptionDispatchInfo`.
        if (context.Response.HasStarted)
        {
            _logger.LogWarning(
                ex,
                "Exception after the response started for {Path}; no response can be written. "
                + "This is expected when a client disconnects mid-stream. "
                + "CorrelationId: {CorrelationId}",
                context.Request.Path,
                correlationId);

            return;
        }

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
