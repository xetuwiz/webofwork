using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using StackExchange.Redis;
using WebOfWork.Api.Data;
using WebOfWork.Api.Integrations.Email;
using WebOfWork.Api.Integrations.Sms;
using WebOfWork.Api.Integrations.Sms.Strategies;
using WebOfWork.Api.Jobs;
using WebOfWork.Api.Services;
using Xunit;

namespace WebOfWork.Api.Tests.Jobs;

public class JobTests
{
    [Fact]
    public async Task CleanupExpiredOtpsJob_DoesNotThrowWhenRedisIsUnavailable()
    {
        var redis = new Mock<IConnectionMultiplexer>();
        redis.Setup(x => x.GetEndPoints(It.IsAny<bool>())).Returns(Array.Empty<EndPoint>());

        var job = new CleanupExpiredOtpsJob(redis.Object, NullLogger<CleanupExpiredOtpsJob>.Instance);

        var act = () => job.ExecuteAsync(CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task AuthService_SendOtpAsync_QueuesOtpEmailJob()
    {
        var options = new DbContextOptionsBuilder<WebOfWorkDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var db = new WebOfWorkDbContext(options);
        var otp = new Mock<IOtpService>();
        otp.Setup(x => x.HasCooldownAsync(It.IsAny<string>())).ReturnsAsync(false);
        otp.Setup(x => x.SetOtpAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan>())).Returns(Task.CompletedTask);
        otp.Setup(x => x.SetCooldownAsync(It.IsAny<string>(), It.IsAny<TimeSpan>())).Returns(Task.CompletedTask);

        var emailSender = new Mock<IEmailSender>();
        var backgroundJobs = new Mock<IBackgroundJobClient>();
        backgroundJobs.Setup(x => x.Create(It.IsAny<Job>(), It.IsAny<IState>())).Returns("job-1");

        // `null!` for the redis multiplexer, deliberately. This is an in-memory
        // test with no Redis container, and `SendOtpAsync` does not touch
        // `_redis` -- that only happens in the registration cache and the
        // account-OTP cooldown. If a future change adds a redis call to this
        // path the test will fail loudly with a NullReferenceException rather
        // than quietly asserting against a stub.
        var service = new AuthService(
            db, otp.Object, new PasswordHasher(), emailSender.Object,
            new RefreshTokenService(), null!, backgroundJobs.Object);

        await service.SendOtpAsync("new-user@example.com");

        otp.Verify(x => x.SetOtpAsync(It.Is<string>(key => key.StartsWith("reg:")), It.IsAny<string>(), It.IsAny<TimeSpan>()), Times.Once);
        backgroundJobs.Verify(x => x.Create(It.IsAny<Job>(), It.IsAny<IState>()), Times.Once);
        emailSender.Verify(x => x.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task PhoneVerificationService_SendPhoneOtpAsync_QueuesPhoneOtpJob()
    {
        var options = new DbContextOptionsBuilder<WebOfWorkDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var db = new WebOfWorkDbContext(options);
        var otp = new Mock<IOtpService>();
        otp.Setup(x => x.SetOtpAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan>())).Returns(Task.CompletedTask);

        var smsSender = new Mock<ISmsSender>();
        var backgroundJobs = new Mock<IBackgroundJobClient>();
        backgroundJobs.Setup(x => x.Create(It.IsAny<Job>(), It.IsAny<IState>())).Returns("job-2");

        var factory = new SmsStrategyFactory();
        var service = new PhoneVerificationService(smsSender.Object, otp.Object, db, factory, backgroundJobs.Object);

        await service.SendPhoneOtpAsync(Guid.NewGuid(), "0779091086");

        otp.Verify(x => x.SetOtpAsync(It.Is<string>(key => key.StartsWith("phone:")), It.IsAny<string>(), It.IsAny<TimeSpan>()), Times.Once);
        backgroundJobs.Verify(x => x.Create(It.IsAny<Job>(), It.IsAny<IState>()), Times.Once);
        smsSender.Verify(x => x.SendSmsAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }
}
