using System;
using System.Threading.Tasks;
using Xunit;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using WebOfWork.Api.Data;
using WebOfWork.Api.Models.Enums;
using WebOfWork.Api.Services;
using StackExchange.Redis;

namespace WebOfWork.Api.Tests.Auth;

[Collection("Auth E2E Collection")]
public class VerifyOtpTests
{
    private readonly AuthTestFixture _fixture;
    public VerifyOtpTests(AuthTestFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task VerifyOtpAndCreateUser_ShouldRejectWrongCode()
    {
        var options = new DbContextOptionsBuilder<WebOfWorkDbContext>().UseSnakeCaseNamingConvention().UseNpgsql(_fixture.Postgres.GetConnectionString()).Options;
        await using var db = new WebOfWorkDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var redis = await ConnectionMultiplexer.ConnectAsync(_fixture.Redis.GetConnectionString());
        var otpService = new OtpService(redis);
        var service = new AuthService(db, otpService, new PasswordHasher(), null!, new RefreshTokenService(), redis);

        // Put a real OTP in Redis
        var email = $"wrongcode_{Guid.NewGuid()}@test.com";
        await otpService.SetOtpAsync($"reg:{email}", "123456", TimeSpan.FromMinutes(10));

        var act = async () => await service.VerifyOtpAndCreateUserAsync(email, "999999", "Pass123!", "New User", UserRole.Client);
        await act.Should().ThrowAsync<UnauthorizedAccessException>().WithMessage("Invalid or expired OTP.");
    }

    [Fact]
    public async Task VerifyOtpAndCreateUser_ShouldCreateAccount_WhenOtpCorrect()
    {
        var options = new DbContextOptionsBuilder<WebOfWorkDbContext>().UseSnakeCaseNamingConvention().UseNpgsql(_fixture.Postgres.GetConnectionString()).Options;
        await using var db = new WebOfWorkDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var redis = await ConnectionMultiplexer.ConnectAsync(_fixture.Redis.GetConnectionString());
        var otpService = new OtpService(redis);
        var hasher = new PasswordHasher();
        var service = new AuthService(db, otpService, hasher, null!, new RefreshTokenService(), redis);

        var email = $"correctcode_{Guid.NewGuid()}@test.com";
        await otpService.SetOtpAsync($"reg:{email}", "123456", TimeSpan.FromMinutes(10));

        var user = await service.VerifyOtpAndCreateUserAsync(email, "123456", "Pass123!", "New User", UserRole.Client);
        
        user.Should().NotBeNull();
        user.Email.Should().Be(email);
        user.IsEmailVerified.Should().BeTrue();
        hasher.VerifyPassword("Pass123!", user.PasswordHash).Should().BeTrue();
    }
}
