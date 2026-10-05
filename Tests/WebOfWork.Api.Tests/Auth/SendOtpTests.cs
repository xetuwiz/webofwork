using System;
using System.Threading.Tasks;
using Xunit;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using WebOfWork.Api.Data;
using WebOfWork.Api.Models;
using WebOfWork.Api.Services;
using StackExchange.Redis;
using Moq;
using WebOfWork.Api.Integrations.Email;

namespace WebOfWork.Api.Tests.Auth;

[Collection("Auth E2E Collection")]
public class SendOtpTests
{
    private readonly AuthTestFixture _fixture;
    public SendOtpTests(AuthTestFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task SendOtpAsync_ShouldRejectDuplicateEmail_WhenUserExists()
    {
        var options = new DbContextOptionsBuilder<WebOfWorkDbContext>().UseSnakeCaseNamingConvention().UseNpgsql(_fixture.Postgres.GetConnectionString()).Options;
        await using var db = new WebOfWorkDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var email = $"duplicate_{Guid.NewGuid()}@test.com";
        db.Users.Add(new User { Email = email, FullName = "Test", IsDeleted = false });
        await db.SaveChangesAsync();

        var redis = await ConnectionMultiplexer.ConnectAsync(_fixture.Redis.GetConnectionString());
        var mockEmail = new Mock<IEmailSender>(); // Mock email just to avoid spamming actual SMTP during unit-level assertions
        var service = new AuthService(db, new OtpService(redis), new PasswordHasher(), mockEmail.Object, new RefreshTokenService(), redis);

        var act = async () => await service.SendOtpAsync(email);
        var ex = await act.Should().ThrowAsync<InvalidOperationException>();
        ex.WithMessage("Email already in use.");
    }

    [Fact]
    public async Task SendOtpAsync_ShouldRejectWhenCooldownActive()
    {
        var options = new DbContextOptionsBuilder<WebOfWorkDbContext>().UseSnakeCaseNamingConvention().UseNpgsql(_fixture.Postgres.GetConnectionString()).Options;
        await using var db = new WebOfWorkDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var email = $"cooldown_{Guid.NewGuid()}@test.com";
        
        var redis = await ConnectionMultiplexer.ConnectAsync(_fixture.Redis.GetConnectionString());
        var mockEmail = new Mock<IEmailSender>();
        var service = new AuthService(db, new OtpService(redis), new PasswordHasher(), mockEmail.Object, new RefreshTokenService(), redis);

        // First attempt should succeed (mock email prevents actual SMTP)
        await service.SendOtpAsync(email);

        // Second attempt immediately after should throw cooldown error
        var act = async () => await service.SendOtpAsync(email);
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("Please wait 60 seconds before requesting another OTP.");
    }
}
