using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using WebOfWork.Api.Authorization;
using WebOfWork.Api.Data;
using WebOfWork.Api.Models;
using WebOfWork.Api.Models.Enums;
using WebOfWork.Api.Services;
using Xunit;

namespace WebOfWork.Api.Tests.Admin;

public sealed class AdminUserTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:15-alpine")
        .WithDatabase("webofwork_admin_user_test")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    public Task InitializeAsync() => _postgres.StartAsync();

    public Task DisposeAsync() => _postgres.DisposeAsync().AsTask();

    [Fact]
    public async Task ToggleUserActiveAsync_PreventsAdminSelfBlock()
    {
        await using var db = await CreateDbContextAsync();
        var admin = new User { Id = Guid.NewGuid(), Email = "admin@test.com", FullName = "Admin User", PasswordHash = "hash", Role = UserRole.Admin, Status = UserStatus.Active };
        db.Users.Add(admin);
        await db.SaveChangesAsync();

        var service = new AdminUserService(db);

        var act = () => service.ToggleUserActiveAsync(admin.Id, CancellationToken.None);

        await act.Should().ThrowExactlyAsync<InvalidOperationException>()
            .WithMessage("*Admin users cannot be blocked or deactivated.*");
    }

    [Fact]
    public async Task SoftDeleteUserAsync_PreventsAdminSelfDeletion()
    {
        await using var db = await CreateDbContextAsync();
        var admin = new User { Id = Guid.NewGuid(), Email = "admin@test.com", FullName = "Admin User", PasswordHash = "hash", Role = UserRole.Admin, Status = UserStatus.Active };
        db.Users.Add(admin);
        await db.SaveChangesAsync();

        var service = new AdminUserService(db);

        var act = () => service.SoftDeleteUserAsync(admin.Id, CancellationToken.None);

        await act.Should().ThrowExactlyAsync<InvalidOperationException>()
            .WithMessage("*Admin users cannot be soft-deleted.*");
    }

    [Fact]
    public async Task GetUsersAsync_FiltersByRoleAndStatus()
    {
        await using var db = await CreateDbContextAsync();
        db.Users.AddRange(
            new User { Id = Guid.NewGuid(), Email = "client1@test.com", FullName = "Client One", PasswordHash = "hash", Role = UserRole.Client, Status = UserStatus.Active },
            new User { Id = Guid.NewGuid(), Email = "freelancer1@test.com", FullName = "Freelancer One", PasswordHash = "hash", Role = UserRole.Freelancer, Status = UserStatus.Active },
            new User { Id = Guid.NewGuid(), Email = "blocked@test.com", FullName = "Blocked User", PasswordHash = "hash", Role = UserRole.Client, Status = UserStatus.Blocked },
            new User { Id = Guid.NewGuid(), Email = "admin@test.com", FullName = "Admin User", PasswordHash = "hash", Role = UserRole.Admin, Status = UserStatus.Active });
        await db.SaveChangesAsync();

        var result = await new AdminUserService(db).GetUsersAsync("client", UserRole.Client, UserStatus.Active, CancellationToken.None);

        result.Should().ContainSingle();
        result[0].Email.Should().Be("client1@test.com");
    }

    [Fact]
    public void AdminAuthorizationPolicy_RequiresAdminRole()
    {
        var options = new AuthorizationOptions();

        AdminAuthorizationPolicy.Configure(options);

        var policy = options.GetPolicy(AuthorizationPolicies.RequireAdmin);
        policy.Should().NotBeNull();
        policy!.Requirements.Should().ContainSingle(r => r is RolesAuthorizationRequirement);

        var rolesRequirement = policy.Requirements
            .OfType<RolesAuthorizationRequirement>()
            .Single();

        rolesRequirement.AllowedRoles.Should().ContainSingle().Which.Should().Be(AuthorizationPolicies.RoleAdmin);
    }

    private async Task<WebOfWorkDbContext> CreateDbContextAsync()
    {
        var options = new DbContextOptionsBuilder<WebOfWorkDbContext>()
            .UseSnakeCaseNamingConvention().UseNpgsql(_postgres.GetConnectionString())
            .Options;
        var db = new WebOfWorkDbContext(options);
        await db.Database.EnsureCreatedAsync();
        return db;
    }
}
