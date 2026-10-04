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
public class UpdateMeTests
{
    private readonly AuthTestFixture _fixture;
    public UpdateMeTests(AuthTestFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task UpdateMeAsync_ShouldTriggerReverification_WhenEmailChanges()
    {
        var options = new DbContextOptionsBuilder<WebOfWorkDbContext>().UseSnakeCaseNamingConvention().UseNpgsql(_fixture.Postgres.GetConnectionString()).Options;
        await using var db = new WebOfWorkDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var id = Guid.NewGuid();
        db.Users.Add(new User { Id = id, Email = $"old_{id}@test.com", FullName = "Test", IsEmailVerified = true });
        await db.SaveChangesAsync();

        var redis = await ConnectionMultiplexer.ConnectAsync(_fixture.Redis.GetConnectionString());
        var service = new AuthService(db, new OtpService(redis), new PasswordHasher(), null!, new RefreshTokenService(), redis);

        var newEmail = $"new_{id}@test.com";
        var user = await service.UpdateMeAsync(id, null, newEmail);

        user.Email.Should().Be($"old_{id}@test.com");
        user.PendingEmail.Should().Be(newEmail);
        user.IsEmailVerified.Should().BeFalse(); // Email changes only after OTP verification.
    }
}
