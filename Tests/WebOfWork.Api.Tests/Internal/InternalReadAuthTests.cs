using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using WebOfWork.Api.Configuration;
using WebOfWork.Api.Middleware;
using Xunit;

namespace WebOfWork.Api.Tests.Internal;

public sealed class InternalReadAuthTests
{
    [Fact]
    public async Task InternalReadWithoutApiKey_Returns401()
    {
        var context = CreateContext("/internal/read/projects/1");
        await InvokeAsync(context, "secret");

        context.Response.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
    }

    [Fact]
    public async Task InternalReadWithUserJwtButNoApiKey_Returns401()
    {
        var context = CreateContext("/internal/read/projects/1");
        context.Request.Headers.Authorization = "Bearer user-jwt";
        await InvokeAsync(context, "secret");

        context.Response.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
    }

    [Fact]
    public async Task InternalReadWithApiKey_ReachesNextMiddleware()
    {
        var context = CreateContext("/internal/read/projects/1");
        context.Request.Headers[InternalApiOptions.HeaderName] = "secret";
        var reachedNext = false;

        await InvokeAsync(context, "secret", _ =>
        {
            reachedNext = true;
            return Task.CompletedTask;
        });

        reachedNext.Should().BeTrue();
    }

    [Fact]
    public async Task SimilarNonInternalPath_IsNotProtectedByInternalMiddleware()
    {
        var context = CreateContext("/internalized/read/projects/1");
        var reachedNext = false;

        await InvokeAsync(context, "secret", _ =>
        {
            reachedNext = true;
            return Task.CompletedTask;
        });

        reachedNext.Should().BeTrue();
    }

    private static DefaultHttpContext CreateContext(string path)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        context.Response.Body = new MemoryStream();
        return context;
    }

    private static Task InvokeAsync(
        DefaultHttpContext context,
        string expectedKey,
        RequestDelegate? next = null)
    {
        var middleware = new InternalApiKeyMiddleware(
            next ?? (_ => Task.CompletedTask),
            Options.Create(new InternalApiOptions { ApiKey = expectedKey }),
            NullLogger<InternalApiKeyMiddleware>.Instance);
        return middleware.InvokeAsync(context);
    }
}