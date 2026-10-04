using System.Threading.RateLimiting;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using WebOfWork.Api.Configuration;
using Xunit;

namespace WebOfWork.Api.Tests.Internal;

public sealed class InternalReadRateLimitTests
{
    [Fact]
    public async Task InternalReadPolicy_AllowsSixtyRequestsAndRejectsTheNext()
    {
        using var limiter = PartitionedRateLimiter.Create<HttpContext, string>(
            InternalReadRateLimitPolicy.Create);
        var context = new DefaultHttpContext();

        for (var index = 0; index < InternalReadRateLimitPolicy.PermitLimit; index++)
        {
            using var lease = await limiter.AcquireAsync(context);
            lease.IsAcquired.Should().BeTrue();
        }

        using var rejectedLease = await limiter.AcquireAsync(context);
        rejectedLease.IsAcquired.Should().BeFalse();
    }
}