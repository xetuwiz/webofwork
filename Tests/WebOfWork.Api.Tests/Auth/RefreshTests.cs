using System;
using System.Threading.Tasks;
using Xunit;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using WebOfWork.Api.Data;
using WebOfWork.Api.Models;
using WebOfWork.Api.Services;
using StackExchange.Redis;

namespace WebOfWork.Api.Tests.Auth;

[Collection("Auth E2E Collection")]
public class RefreshTests
{
    private readonly AuthTestFixture _fixture;
    public RefreshTests(AuthTestFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task UpdateRefreshToken_ShouldSetTokenAndExpiry()
    {
        var options = new DbContextOptionsBuilder<WebOfWorkDbContext>().UseSnakeCaseNamingConvention().UseNpgsql(_fixture.Postgres.GetConnectionString()).Options;
        await using var db = new WebOfWorkDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var id = Guid.NewGuid();
        db.Users.Add(new User { Id = id, Email = $"refresh_{id}@test.com", FullName = "Test" });
        await db.SaveChangesAsync();

        var redis = await ConnectionMultiplexer.ConnectAsync(_fixture.Redis.GetConnectionString());
        var service = new AuthService(db, new OtpService(redis), new PasswordHasher(), null!, new RefreshTokenService(), redis);

        var expiry = DateTime.UtcNow.AddDays(7);
        await service.UpdateRefreshTokenAsync(id, "NEW_REAL_TOKEN", expiry);
        
        var user = await db.Users.FindAsync(id);
        user!.RefreshToken.Should().Be("NEW_REAL_TOKEN");
        user.RefreshTokenExpiry.Should().Be(expiry);
    }
}
