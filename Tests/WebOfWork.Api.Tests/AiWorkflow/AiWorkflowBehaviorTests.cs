using FluentAssertions;
using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using Microsoft.EntityFrameworkCore;
using Moq;
using WebOfWork.Api.Data;
using WebOfWork.Api.Models;
using WebOfWork.Api.Models.Enums;
using WebOfWork.Api.Services;
using Xunit;

namespace WebOfWork.Api.Tests.AiWorkflow;

public sealed class AiWorkflowBehaviorTests
{
    [Fact]
    public async Task WorkflowService_PersistsAllArtifactTypes_AndCompletesRun()
    {
        await using var db = CreateDb();
        var requester = new User
        {
            Id = Guid.NewGuid(),
            Email = "workflow-artifacts@example.com",
            FullName = "Workflow Client",
            PasswordHash = "hash",
            Role = UserRole.Client,
            Status = UserStatus.Active,
            IsEmailVerified = true
        };
        db.Users.Add(requester);
        await db.SaveChangesAsync();

        var service = new AiWorkflowService(db, CreateJobs().Object);
        // The stage is now a required argument. `validate` is the stage whose
        // success parks the run on the manager gate, which is what the assertions
        // below about approval depend on.
        var run = await service.StartWorkflowAsync(requester.Id, "Persist artifacts", WorkflowStages.Validate);

        await service.AppendStepLogAsync(run.Id, "analysis", "agent", "input", "output", "Running", 10);
        await service.AppendToolCallLogAsync(run.Id, "search", "query", "result", true, 20);
        await service.AppendAgentRecommendationAsync(run.Id, new AgentRecommendation
        {
            ProjectId = 1,
            FreelancerId = Guid.NewGuid(),
            Score = 0.95m,
            FactorsJson = "{\"skill\":true}",
            Reason = "Strong match",
            PortfolioSamplesJson = "[\"sample\"]"
        });
        await service.AppendProposalScreeningAsync(run.Id, new ProposalScreening
        {
            ProposalId = 1,
            FitScore = 0.9m,
            StrengthsJson = "[\"experience\"]",
            ConcernsJson = "[]",
            Recommendation = "accept",
            Rationale = "Good fit"
        });
        await service.AppendValidationResultAsync(run.Id, new ValidationResult
        {
            ProjectId = 1,
            ProposalId = 1,
            FreelancerId = Guid.NewGuid(),
            Valid = true,
            IssuesJson = "[]",
            CostBreakdownJson = "{\"amount\":1500,\"currency\":\"USD\"}",
            RequiresHumanApproval = false,
            HumanSummary = "Validated"
        });
        await service.CompleteWorkflowAsync(run.Id, "Completed");

        (await db.WorkflowStepLogs.CountAsync()).Should().Be(1);
        (await db.ToolCallLogs.CountAsync()).Should().Be(1);
        (await db.AgentRecommendations.CountAsync()).Should().Be(1);
        (await db.ProposalScreenings.CountAsync()).Should().Be(1);
        (await db.ValidationResults.CountAsync()).Should().Be(1);
        (await db.WorkflowRuns.SingleAsync()).Status.Should().Be(WorkflowRunStatus.Completed);
    }

    [Fact]
    public async Task ApproveWorkflow_CreatesOfferedUsdContract_AndQueuesNotification()
    {
        await using var db = CreateDb();
        var clientUser = NewUser(UserRole.Client, "workflow-client@example.com");
        var freelancerUser = NewUser(UserRole.Freelancer, "workflow-freelancer@example.com");
        var client = new ClientProfile { Id = Guid.NewGuid(), UserId = clientUser.Id, User = clientUser, CompanyName = "Client", Location = "Colombo" };
        var freelancer = new FreelancerProfile { Id = Guid.NewGuid(), UserId = freelancerUser.Id, User = freelancerUser, IsActive = true };
        var project = new Project
        {
            Client = client,
            ClientId = client.Id,
            Title = "Workflow contract",
            Description = "Description",
            Requirements = "Requirements",
            ExpectedDeliverables = "Deliverables",
            PricingType = ProjectPricingType.Fixed,
            FixedPrice = 1500m,
            ExpectedDays = 10,
            Status = ProjectStatus.Open
        };
        db.Users.AddRange(clientUser, freelancerUser);
        db.ClientProfiles.Add(client);
        db.FreelancerProfiles.Add(freelancer);
        db.Projects.Add(project);
        await db.SaveChangesAsync();

        var run = new WorkflowRun { Objective = "Create contract", ProjectId = project.Id, RequestedByUserId = clientUser.Id };
        db.WorkflowRuns.Add(run);
        await db.SaveChangesAsync();

        var jobs = CreateJobs();
        var service = new AiWorkflowService(db, jobs.Object);
        // The contract spec is the 6th parameter, not the 4th. Argument 4 is
        // `rationale`; passing the spec there was a silent mis-order that the
        // compiler caught only after `rationale` was retyped.
        await service.ApproveWorkflowAsync(
            run.Id, Guid.NewGuid(), "Approved", null,
            new WorkflowContractSpec(freelancer.Id, 1500m));

        var contract = await db.Contracts.SingleAsync();
        contract.Status.Should().Be(ContractStatus.Offered);
        contract.TotalAmount.Should().Be(1500m);
        contract.ClientId.Should().Be(client.Id);
        contract.FreelancerId.Should().Be(freelancer.Id);
        jobs.Verify(x => x.Create(It.IsAny<Job>(), It.IsAny<IState>()), Times.Once);
    }

    private static User NewUser(UserRole role, string email) => new()
    {
        Id = Guid.NewGuid(),
        Email = email,
        FullName = role.ToString(),
        PasswordHash = "hash",
        Role = role,
        Status = UserStatus.Active,
        IsEmailVerified = true
    };

    private static WebOfWorkDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<WebOfWorkDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new WebOfWorkDbContext(options);
    }

    private static Mock<IBackgroundJobClient> CreateJobs()
    {
        var jobs = new Mock<IBackgroundJobClient>();
        jobs.Setup(x => x.Create(It.IsAny<Job>(), It.IsAny<IState>())).Returns("job-id");
        return jobs;
    }
}
