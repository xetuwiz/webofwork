using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Testcontainers.PostgreSql;
using WebOfWork.Api.Data;
using WebOfWork.Api.DTOs.Support;
using WebOfWork.Api.Models;
using WebOfWork.Api.Models.Enums;
using WebOfWork.Api.Services;
using Xunit;

namespace WebOfWork.Api.Tests.Support;

public sealed class SupportTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:15-alpine")
        .WithDatabase("webofwork_support_test")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    public Task InitializeAsync() => _postgres.StartAsync();

    public Task DisposeAsync() => _postgres.DisposeAsync().AsTask();

    [Fact]
    public async Task CreateTicketAsync_RejectsSixthOpenTicket()
    {
        await using var db = await CreateDbContextAsync();
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "support-user@test.com",
            FullName = "Support User",
            PasswordHash = "hash",
            Role = UserRole.Client
        };
        db.Users.Add(user);

        for (var index = 0; index < SupportService.MaxOpenTicketsPerUser; index++)
        {
            db.SupportTickets.Add(new SupportTicket
            {
                UserId = user.Id,
                SenderRole = UserRole.Client,
                Subject = $"Subject {index}",
                Message = $"Message {index}",
                Status = SupportTicketStatus.Open
            });
        }

        await db.SaveChangesAsync();

        var service = new SupportService(
            db,
            NullLogger<SupportService>.Instance,
            new Mock<Hangfire.IBackgroundJobClient>().Object);

        var act = () => service.CreateTicketAsync(
            user.Id,
            UserRole.Client,
            new CreateSupportTicketRequest
            {
                Subject = "Sixth subject",
                Message = "Sixth message"
            },
            CancellationToken.None);

        await act.Should().ThrowExactlyAsync<InvalidOperationException>()
            .WithMessage("*maximum limit of 5 active support tickets*");
    }

    [Fact]
    public async Task CancelMyTicketAsync_AllowsOwnerToCancelOpenTicket()
    {
        await using var db = await CreateDbContextAsync();
        var user = await AddUserAsync(db, UserRole.Client);
        var service = CreateService(db);
        var ticket = await service.CreateTicketAsync(
            user.Id,
            UserRole.Client,
            new CreateSupportTicketRequest { Subject = "Subject", Message = "Message" },
            CancellationToken.None);

        var result = await service.CancelMyTicketAsync(user.Id, ticket.Id, CancellationToken.None);

        // Enum, not string, for the same reason as the DTOs elsewhere.
        result.Status.Should().Be(SupportTicketStatus.Cancelled);
        (await db.SupportTickets.SingleAsync(t => t.Id == ticket.Id)).Status
            .Should().Be(SupportTicketStatus.Cancelled);
    }

    [Fact]
    public async Task CancelMyTicketAsync_RejectsNonOwner()
    {
        await using var db = await CreateDbContextAsync();
        var owner = await AddUserAsync(db, UserRole.Client);
        var outsider = await AddUserAsync(db, UserRole.Freelancer);
        var service = CreateService(db);
        var ticket = await service.CreateTicketAsync(
            owner.Id,
            UserRole.Client,
            new CreateSupportTicketRequest { Subject = "Subject", Message = "Message" },
            CancellationToken.None);

        var act = () => service.CancelMyTicketAsync(outsider.Id, ticket.Id, CancellationToken.None);

        await act.Should().ThrowExactlyAsync<UnauthorizedAccessException>();
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

    private static async Task<User> AddUserAsync(WebOfWorkDbContext db, UserRole role)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = $"{Guid.NewGuid():N}@test.com",
            FullName = "Support User",
            PasswordHash = "hash",
            Role = role
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    private static SupportService CreateService(WebOfWorkDbContext db) => new(
        db,
        NullLogger<SupportService>.Instance,
        new Mock<Hangfire.IBackgroundJobClient>().Object);
}