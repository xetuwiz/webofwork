using System;
using System.Threading.Tasks;
using Xunit;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using WebOfWork.Api.Data;
using WebOfWork.Api.Models;
using WebOfWork.Api.Models.Enums;
using WebOfWork.Api.Services;
using StackExchange.Redis;

namespace WebOfWork.Api.Tests.Auth;

[Collection("Auth E2E Collection")]
public class LoginTests
{
    private readonly AuthTestFixture _fixture;
    public LoginTests(AuthTestFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task LoginAsync_ShouldRejectBlockedUser()
    {
        var options = new DbContextOptionsBuilder<WebOfWorkDbContext>().UseSnakeCaseNamingConvention().UseNpgsql(_fixture.Postgres.GetConnectionString()).Options;
        await using var db = new WebOfWorkDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var email = $"blocked_{Guid.NewGuid()}@test.com";
        var hasher = new PasswordHasher();
        db.Users.Add(new User { Email = email, FullName = "Test", Status = UserStatus.Blocked, PasswordHash = hasher.HashPassword("Pass123!") });
        await db.SaveChangesAsync();

        var redis = await ConnectionMultiplexer.ConnectAsync(_fixture.Redis.GetConnectionString());
        var service = new AuthService(db, new OtpService(redis), hasher, null!, new RefreshTokenService(), redis);

        var act = async () => await service.LoginAsync(email, "Pass123!");
        await act.Should().ThrowAsync<UnauthorizedAccessException>().WithMessage("Account blocked.");
    }

    [Fact]
    public async Task LoginAsync_ShouldRejectWrongPassword()
    {
        var options = new DbContextOptionsBuilder<WebOfWorkDbContext>().UseSnakeCaseNamingConvention().UseNpgsql(_fixture.Postgres.GetConnectionString()).Options;
        await using var db = new WebOfWorkDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var email = $"wrongpass_{Guid.NewGuid()}@test.com";
        var hasher = new PasswordHasher();
        // REAL HASHING
        db.Users.Add(new User { Email = email, FullName = "Test", Status = UserStatus.Active, IsEmailVerified = true, PasswordHash = hasher.HashPassword("CorrectPass123!") });
        await db.SaveChangesAsync();
        
        var redis = await ConnectionMultiplexer.ConnectAsync(_fixture.Redis.GetConnectionString());
        var service = new AuthService(db, new OtpService(redis), hasher, null!, new RefreshTokenService(), redis);

        // ACT WITH WRONG PASSWORD
        var act = async () => await service.LoginAsync(email, "WrongPassword999!");
        await act.Should().ThrowAsync<UnauthorizedAccessException>().WithMessage("Invalid credentials.");
    }
}
