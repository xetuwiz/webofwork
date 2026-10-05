using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using WebOfWork.Api.Configuration;
using WebOfWork.Api.Middleware;
using Xunit;

namespace WebOfWork.Api.Tests.Internal;

public sealed class InternalMiddlewareOrderTests
{
    [Fact]
    public async Task InvalidInternalRequest_IsRejectedBeforeNextMiddleware()
    {
        var context = new DefaultHttpContext();
        context.Request.Path = "/internal/read/platform/commission";
        context.Response.Body = new MemoryStream();
        var reachedNext = false;
        var middleware = new InternalApiKeyMiddleware(
            _ =>
            {
                reachedNext = true;
                return Task.CompletedTask;
            },
            Options.Create(new InternalApiOptions { ApiKey = "secret" }),
            NullLogger<InternalApiKeyMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
        reachedNext.Should().BeFalse();
    }

    [Fact]
    public async Task ValidInternalRequest_ReachesNextBeforeMvc()
    {
        var context = new DefaultHttpContext();
        context.Request.Path = "/internal/read/platform/commission";
        context.Request.Headers[InternalApiOptions.HeaderName] = "secret";
        var reachedNext = false;
        var middleware = new InternalApiKeyMiddleware(
            _ =>
            {
                reachedNext = true;
                return Task.CompletedTask;
            },
            Options.Create(new InternalApiOptions { ApiKey = "secret" }),
            NullLogger<InternalApiKeyMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        reachedNext.Should().BeTrue();
    }
}