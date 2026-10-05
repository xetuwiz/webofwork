using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using WebOfWork.Api.Data;
using WebOfWork.Api.Models;
using WebOfWork.Api.Models.Enums;
using Xunit;


namespace WebOfWork.Api.Tests.AiWorkflow;

[Trait("Category", "Container")]
public class WorkflowRunPersistenceTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:15-alpine")
        .WithDatabase("webofwork_ai_workflow_test")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    public async Task InitializeAsync() => await _postgres.StartAsync();
    public async Task DisposeAsync() => await _postgres.DisposeAsync();

    [Fact]
    public async Task WorkflowRun_CanBePersisted_WithStatusPendingApproval()
    {
        var options = new DbContextOptionsBuilder<WebOfWorkDbContext>()
            .UseSnakeCaseNamingConvention().UseNpgsql(_postgres.GetConnectionString())
            .Options;

        await using var db = new WebOfWorkDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var requestedBy = new User
        {
            Id = Guid.NewGuid(),
            Email = $"workflow-{Guid.NewGuid():N}@example.com",
            FullName = "Workflow Requester",
            PasswordHash = "hash",
            Role = UserRole.Client,
            Status = UserStatus.Active,
            IsEmailVerified = true
        };
        db.Users.Add(requestedBy);
        await db.SaveChangesAsync();

        var clientProfile = new ClientProfile
        {
            Id = Guid.NewGuid(),
            UserId = requestedBy.Id,
            User = requestedBy,
            CompanyName = "Acme Studio",
            Location = "Colombo"
        };
        db.ClientProfiles.Add(clientProfile);

        var project = new Project
        {
            ClientId = clientProfile.Id,
            Client = clientProfile,
            Title = "AI Workflow Pilot",
            Description = "A validation project for workflow persistence.",
            Requirements = "None",
            ExpectedDeliverables = "Workflow validation",
            PricingType = ProjectPricingType.Fixed,
            FixedPrice = 1500m,
            ExpectedDays = 14,
            Status = ProjectStatus.Open
        };
        db.Projects.Add(project);
        await db.SaveChangesAsync();

        var run = new WorkflowRun
        {
            Objective = "Auto-match a brief to freelancers",
            Plan = "[\"brief_analysis\", \"matching\"]",
            Status = WorkflowRunStatus.PendingApproval,
            ProjectId = project.Id,
            RequestedByUserId = requestedBy.Id,
            ApprovalDecision = "Pending"
        };

        db.WorkflowRuns.Add(run);
        await db.SaveChangesAsync();

        (await db.WorkflowRuns.CountAsync()).Should().Be(1);
        run.Id.Should().BeGreaterThan(0);
        run.Status.Should().Be(WorkflowRunStatus.PendingApproval);
    }
}
