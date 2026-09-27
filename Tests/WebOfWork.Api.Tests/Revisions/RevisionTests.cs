using System;
using System.Threading.Tasks;
using Xunit;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using WebOfWork.Api.Data;
using WebOfWork.Api.DTOs.Revisions;
using WebOfWork.Api.Models;
using WebOfWork.Api.Models.Enums;
using WebOfWork.Api.Services;

namespace WebOfWork.Api.Tests.Revisions;

public class RevisionTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:15-alpine")
        .WithDatabase("webofwork_test")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
    }

    public async Task DisposeAsync()
    {
        await _postgres.DisposeAsync();
    }

    private async Task<WebOfWorkDbContext> GetDbContextAsync()
    {
        var options = new DbContextOptionsBuilder<WebOfWorkDbContext>()
            .UseSnakeCaseNamingConvention().UseNpgsql(_postgres.GetConnectionString())
            .Options;
        var db = new WebOfWorkDbContext(options);
        await db.Database.EnsureCreatedAsync();
        return db;
    }

    private async Task<(User Client, User Freelancer, Contract Contract)> CreateSetupDataAsync(WebOfWorkDbContext db)
    {
        var clientUser = new User { Id = Guid.NewGuid(), Email = $"client_{Guid.NewGuid()}@test.com", FullName = "Client" };
        var freelancerUser = new User { Id = Guid.NewGuid(), Email = $"freelancer_{Guid.NewGuid()}@test.com", FullName = "Freelancer" };

        var clientProfile = new ClientProfile { Id = Guid.NewGuid(), UserId = clientUser.Id, User = clientUser };
        var freelancerProfile = new FreelancerProfile { Id = Guid.NewGuid(), UserId = freelancerUser.Id, User = freelancerUser };

        var project = new Project { Title = "Test Project", ClientId = clientProfile.Id };

        var contract = new Contract
        {
            Project = project,
            ClientId = clientProfile.Id,
            Client = clientProfile,
            FreelancerId = freelancerProfile.Id,
            Freelancer = freelancerProfile,
            TotalAmount = 1000m,
            Status = ContractStatus.Active
        };

        db.Users.AddRange(clientUser, freelancerUser);
        db.ClientProfiles.Add(clientProfile);
        db.FreelancerProfiles.Add(freelancerProfile);
        db.Projects.Add(project);
        db.Contracts.Add(contract);
        await db.SaveChangesAsync();

        return (clientUser, freelancerUser, contract);
    }

    [Fact]
    public async Task RequestRevision_AsFreelancer_ReturnsForbidden()
    {
        await using var db = await GetDbContextAsync();
        var data = await CreateSetupDataAsync(db);
        var service = new RevisionService(db);

        var request = new RequestRevisionRequest("Reason");

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.RequestRevisionAsync(data.Contract.Id, data.Freelancer.Id, request));
    }

    [Fact]
    public async Task ActionRevision_AsClient_ReturnsForbidden()
    {
        await using var db = await GetDbContextAsync();
        var data = await CreateSetupDataAsync(db);

        var revision = new ContractRevision
        {
            ContractId = data.Contract.Id,
            UserId = data.Client.Id,
            Reason = "Fix this",
            Status = RevisionStatus.Pending
        };
        db.ContractRevisions.Add(revision);
        await db.SaveChangesAsync();

        var service = new RevisionService(db);
        var request = new RevisionActionRequest(true, null);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.ActionRevisionAsync(revision.Id, data.Client.Id, request));
    }
}
