using Microsoft.AspNetCore.Http;

namespace WebOfWork.Api.Middleware;

public class JwtQueryStringMiddleware
{
    private const string SignalRQueryKey = "access_token";
    private readonly RequestDelegate _next;

    public JwtQueryStringMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Query.TryGetValue(SignalRQueryKey, out var token)
            && !string.IsNullOrWhiteSpace(token)
            && string.IsNullOrWhiteSpace(context.Request.Headers.Authorization))
        {
            context.Request.Headers.Authorization = $"Bearer {token}";
        }
        return _next(context);
    }
}
