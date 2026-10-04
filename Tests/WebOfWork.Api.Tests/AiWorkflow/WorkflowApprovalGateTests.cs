using FluentAssertions;
using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using Microsoft.EntityFrameworkCore;
using Moq;
using WebOfWork.Api.Data;
using WebOfWork.Api.DTOs.Invitations;
using WebOfWork.Api.Models;
using WebOfWork.Api.Models.Enums;
using WebOfWork.Api.Services;
using Xunit;

namespace WebOfWork.Api.Tests.AiWorkflow;

/// <summary>
/// The two approval gates, and specifically that they cannot be confused with
/// each other.
/// </summary>
/// <remarks>
/// The bug these guard against is real and already happened: one
/// <c>ApproveWorkflowAsync</c> served both gates, it created a contract on every
/// approval, and both gates shared the <c>PendingApproval</c> status. A client
/// approving a shortlist got a contract drafted with whichever freelancer the model
/// ranked first.
/// </remarks>
public class WorkflowApprovalGateTests
{
    // ── the manager gate refuses a shortlist approval ─────────────────────

    [Fact]
    public async Task ManagerGate_RefusesARunWaitingOnItsClient()
    {
        await using var db = InMemory(nameof(ManagerGate_RefusesARunWaitingOnItsClient));
        var run = await SeedRunAsync(db, WorkflowRunStatus.PendingClientApproval);

        var service = NewService(db);

        var act = async () => await service.ApproveWorkflowAsync(
            run.Id, Guid.NewGuid(), contractSpec: new WorkflowContractSpec(Guid.NewGuid(), 100m));

        // The error has to name the right endpoint, or the caller retries the same
        // call and gets the same failure.
        var ex = await act.Should().ThrowAsync<InvalidOperationException>();
        ex.Which.Message.Should().Contain("shortlist");
    }

    [Fact]
    public async Task ManagerGate_Refusal_LeavesNoContractBehind()
    {
        await using var db = InMemory(nameof(ManagerGate_Refusal_LeavesNoContractBehind));
        var run = await SeedRunAsync(db, WorkflowRunStatus.PendingClientApproval);

        var service = NewService(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ApproveWorkflowAsync(
            run.Id, Guid.NewGuid(), contractSpec: new WorkflowContractSpec(Guid.NewGuid(), 100m)));

        (await db.Contracts.CountAsync()).Should().Be(0);
        (await db.WorkflowRuns.FindAsync(run.Id))!.Status
            .Should().Be(WorkflowRunStatus.PendingClientApproval, "the run is untouched");
    }

    [Fact]
    public async Task ManagerGate_AllowsRejectionOfAClientGatedRun()
    {
        await using var db = InMemory(nameof(ManagerGate_AllowsRejectionOfAClientGatedRun));
        var run = await SeedRunAsync(db, WorkflowRunStatus.PendingClientApproval);

        var service = NewService(db);
        var result = await service.ApproveWorkflowAsync(run.Id, Guid.NewGuid(), decision: "Rejected");

        // Someone must always be able to stop a run, whoever owns the gate.
        result.Status.Should().Be(WorkflowRunStatus.Rejected);
    }

    [Fact]
    public async Task ManagerGate_AllowsClearingAStuckRunThatNeverReachedTheAgent()
    {
        await using var db = InMemory(nameof(ManagerGate_AllowsClearingAStuckRunThatNeverReachedTheAgent));
        // No agent service, a timeout, a crashed process: the run sits at Pending
        // forever. Refusing non-approval statuses would make it unrescuable.
        var run = await SeedRunAsync(db, WorkflowRunStatus.Pending);

        var service = NewService(db);
        var result = await service.ApproveWorkflowAsync(run.Id, Guid.NewGuid());

        result.Status.Should().Be(WorkflowRunStatus.Approved);
    }

    // ── the client gate ───────────────────────────────────────────────────

    [Fact]
    public async Task ClientGate_RecordsTheDecisionBeforeInvitingAnyone()
    {
        await using var db = InMemory(nameof(ClientGate_RecordsTheDecisionBeforeInvitingAnyone));
        var run = await SeedRunAsync(db, WorkflowRunStatus.PendingClientApproval);

        // Captures the run's state at the moment the first invitation is created.
        // That ordering is the whole point: a crash mid-loop must not lose the
        // human's decision.
        string? resumeValueAtFirstInvite = null;
        var invitations = new Mock<IInvitationService>();
        invitations
            .Setup(i => i.CreateInvitationAsync(It.IsAny<Guid>(), It.IsAny<CreateInvitationRequest>()))
            .Returns(async () =>
            {
                resumeValueAtFirstInvite ??= (await db.WorkflowRuns.FindAsync(run.Id))!.ResumeValue;
                return new InvitationResponse { Id = 1 };
            });

        var service = NewService(db, invitations.Object);
        var freelancerId = await SeedInvitableFreelancerAsync(db);
        var result = await service.ApproveShortlistAsync(run.Id, run.RequestedByUserId, new[] { freelancerId });

        resumeValueAtFirstInvite.Should().NotBeNull("the decision must be durable before the first invite");
        // camelCase, because this jsonb is read by the Python agent service and
        // the rest of the API is camelCase on the wire.
        resumeValueAtFirstInvite.Should().Contain("freelancerIds");
        result.InvitationIds.Should().ContainSingle();
    }

    [Fact]
    public async Task ClientGate_RefusesAnyoneButThePostingClient()
    {
        await using var db = InMemory(nameof(ClientGate_RefusesAnyoneButThePostingClient));
        var run = await SeedRunAsync(db, WorkflowRunStatus.PendingClientApproval);

        var invitations = new Mock<IInvitationService>();
        var service = NewService(db, invitations.Object);

        var act = async () => await service.ApproveShortlistAsync(
            run.Id, Guid.NewGuid(), new[] { Guid.NewGuid() });

        // Without this, any signed-in user who guessed a run id could send
        // invitations out of someone else's account.
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        invitations.Verify(
            i => i.CreateInvitationAsync(It.IsAny<Guid>(), It.IsAny<CreateInvitationRequest>()),
            Times.Never);
    }

    [Fact]
    public async Task ClientGate_RejectsARunThatIsNotWaitingOnTheClient()
    {
        await using var db = InMemory(nameof(ClientGate_RejectsARunThatIsNotWaitingOnTheClient));
        var run = await SeedRunAsync(db, WorkflowRunStatus.PendingManagerApproval);

        var service = NewService(db);
        var freelancerId = await SeedInvitableFreelancerAsync(db);
        var act = async () => await service.ApproveShortlistAsync(run.Id, run.RequestedByUserId, new[] { freelancerId });

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task ClientGate_RejectsAnEmptyShortlist()
    {
        await using var db = InMemory(nameof(ClientGate_RejectsAnEmptyShortlist));
        var run = await SeedRunAsync(db, WorkflowRunStatus.PendingClientApproval);

        var service = NewService(db);
        var act = async () => await service.ApproveShortlistAsync(run.Id, run.RequestedByUserId, Array.Empty<Guid>());

        // Approving nothing would end the run having contacted nobody.
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task ClientGate_RejectsAnOversizedShortlist()
    {
        await using var db = InMemory(nameof(ClientGate_RejectsAnOversizedShortlist));
        var run = await SeedRunAsync(db, WorkflowRunStatus.PendingClientApproval);

        var service = NewService(db);
        var tooMany = Enumerable.Range(0, AiWorkflowService.MaxShortlistApprovals + 1)
                                .Select(_ => Guid.NewGuid()).ToList();

        var act = async () => await service.ApproveShortlistAsync(run.Id, run.RequestedByUserId, tooMany);

        // Each name becomes an invitation plus a notification.
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task ClientGate_DeduplicatesSoNoOneIsInvitedTwice()
    {
        await using var db = InMemory(nameof(ClientGate_DeduplicatesSoNoOneIsInvitedTwice));
        var run = await SeedRunAsync(db, WorkflowRunStatus.PendingClientApproval);
        // A freelancer who exists and has a package, so the approval reaches
        // the invitation call at all.
        var freelancer = await SeedInvitableFreelancerAsync(db);

        var invitations = new Mock<IInvitationService>();
        invitations
            .Setup(i => i.CreateInvitationAsync(It.IsAny<Guid>(), It.IsAny<CreateInvitationRequest>()))
            .Returns(() => Task.FromResult(new InvitationResponse { Id = 1 }));

        var service = NewService(db, invitations.Object);
        var result = await service.ApproveShortlistAsync(
            run.Id, run.RequestedByUserId, new[] { freelancer, freelancer, freelancer });

        invitations.Verify(
            i => i.CreateInvitationAsync(It.IsAny<Guid>(), It.IsAny<CreateInvitationRequest>()),
            Times.Once);
        result.InvitationIds.Should().ContainSingle();
    }

    [Fact]
    public async Task ClientGate_PartialFailure_StillInvitesEveryoneElse()
    {
        await using var db = InMemory(nameof(ClientGate_PartialFailure_StillInvitesEveryoneElse));
        var run = await SeedRunAsync(db, WorkflowRunStatus.PendingClientApproval);

        // Both must be real, invitable freelancers: the point of this test is that
        // one invitation failing does not cost the other, not that a missing
        // freelancer is handled gracefully.
        var good = await SeedInvitableFreelancerAsync(db);
        var alreadyProposed = await SeedInvitableFreelancerAsync(db);
        var call = 0;

        var invitations = new Mock<IInvitationService>();
        invitations
            .Setup(i => i.CreateInvitationAsync(It.IsAny<Guid>(), It.IsAny<CreateInvitationRequest>()))
            .Returns(() =>
            {
                // Mirrors the real service, which refuses a freelancer who has
                // already proposed -- routine in a shortlist of people who applied.
                if (Interlocked.Increment(ref call) == 1)
                    throw new InvalidOperationException("Freelancer has already submitted a proposal for this project.");
                return Task.FromResult(new InvitationResponse { Id = 42 });
            });

        var service = NewService(db, invitations.Object);
        var result = await service.ApproveShortlistAsync(
            run.Id, run.RequestedByUserId, new[] { alreadyProposed, good });

        // The client approved two people and should not receive zero invitations
        // because one of them had already applied.
        result.InvitationIds.Should().ContainSingle();
        result.Skipped.Should().ContainSingle();
        result.Skipped[0].FreelancerId.Should().Be(alreadyProposed);
        result.Skipped[0].Reason.Should().Contain("already submitted");
        result.Run.Status.Should().Be(WorkflowRunStatus.Approved);
    }

    [Fact]
    public async Task ClientGate_TotalFailure_MarksTheRunFailed()
    {
        await using var db = InMemory(nameof(ClientGate_TotalFailure_MarksTheRunFailed));
        var run = await SeedRunAsync(db, WorkflowRunStatus.PendingClientApproval);

        var invitations = new Mock<IInvitationService>();
        invitations
            .Setup(i => i.CreateInvitationAsync(It.IsAny<Guid>(), It.IsAny<CreateInvitationRequest>()))
            .ThrowsAsync(new InvalidOperationException("Freelancer not found."));

        var service = NewService(db, invitations.Object);
        var result = await service.ApproveShortlistAsync(
            run.Id, run.RequestedByUserId, new[] { Guid.NewGuid() });

        // Not a partial success: the client asked to contact people and nobody was
        // contacted.
        result.InvitationIds.Should().BeEmpty();
        result.Run.Status.Should().Be(WorkflowRunStatus.Failed);
    }

    // ── resume ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Resume_RefusesARunThatIsNotPaused()
    {
        await using var db = InMemory(nameof(Resume_RefusesARunThatIsNotPaused));
        var run = await SeedRunAsync(db, WorkflowRunStatus.Pending);

        var service = NewService(db);
        var act = async () => await service.ResumeWorkflowAsync(run.Id, run.RequestedByUserId, "{}");

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Resume_RefusesWhenNoDecisionWasRecorded()
    {
        await using var db = InMemory(nameof(Resume_RefusesWhenNoDecisionWasRecorded));
        var run = await SeedRunAsync(db, WorkflowRunStatus.PendingManagerApproval);
        run.ResumeValue = null;

        var service = NewService(db);
        var act = async () => await service.ResumeWorkflowAsync(run.Id, run.RequestedByUserId, resumeValue: null);

        // Resuming with nothing to resume from would re-run the graph from the
        // start and recompute a decision a human already made.
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Resume_PrefersTheRecordedDecisionOverTheArgument()
    {
        await using var db = InMemory(nameof(Resume_PrefersTheRecordedDecisionOverTheArgument));
        var run = await SeedRunAsync(db, WorkflowRunStatus.PendingClientApproval);
        run.ResumeValue = "{\"freelancerIds\":[\"recorded\"]}";

        var service = NewService(db);
        await service.ResumeWorkflowAsync(run.Id, run.RequestedByUserId, "{\"freelancerIds\":[\"stale\"]}");

        // A client re-sending a stale body must not overwrite a decision already
        // made and durably recorded.
        (await db.WorkflowRuns.FindAsync(run.Id))!.ResumeValue
            .Should().Contain("recorded");
    }

    // ── fixtures ──────────────────────────────────────────────────────────

    /// <summary>
    /// A freelancer who can actually be invited, and their profile id.
    /// </summary>
    /// <remarks>
    /// An invitation is priced by a package, so approving a shortlist resolves one
    /// before creating anything. These tests used to pass a bare
    /// <c>Guid.NewGuid()</c> -- a freelancer who does not exist, with no package --
    /// and passed anyway, because the mocked <c>IInvitationService</c> accepted
    /// any request at all. That is exactly how the missing-package defect survived
    /// for so long: the mock could not refuse a malformed request.
    ///
    /// The fixture is realistic now, which means these tests are about what they
    /// claim to be about -- decision durability, deduplication, partial failure --
    /// rather than about the mock's permissiveness.
    /// </remarks>
    private static async Task<Guid> SeedInvitableFreelancerAsync(WebOfWorkDbContext db)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = $"invitable-{Guid.NewGuid():N}@test.com",
            FullName = "Invitable Freelancer",
            PasswordHash = "x",
            Role = UserRole.Freelancer,
            CurrentRole = UserRole.Freelancer,
            Status = UserStatus.Active,
        };

        var profile = new FreelancerProfile
        {
            Id = Guid.NewGuid(),
            User = user,
            Tagline = "Tagline",
            Bio = "Bio",
            Phone = "0770000009",
            IsPhoneVerified = true,
            IsActive = true,
            Availability = true,
        };

        db.Users.Add(user);
        db.FreelancerProfiles.Add(profile);
        db.FreelancerPackages.Add(new FreelancerPackage
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            PackageType = "starter",
            Price = 500m,
            DeliveryDays = 5,
            Description = "Starter package",
        });

        await db.SaveChangesAsync();
        return profile.Id;
    }

    private static WebOfWorkDbContext InMemory(string name) =>
        new(new DbContextOptionsBuilder<WebOfWorkDbContext>()
            .UseInMemoryDatabase(name)
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options);

    private static AiWorkflowService NewService(
        WebOfWorkDbContext db, IInvitationService? invitations = null)
    {
        var jobs = new Mock<IBackgroundJobClient>();
        jobs.Setup(j => j.Create(It.IsAny<Job>(), It.IsAny<IState>())).Returns("job-1");
        return new AiWorkflowService(db, jobs.Object, null, null, invitations);
    }

    /// <summary>
    /// A run owned by a real client who posted a real project, so the ownership
    /// and project checks have something to pass against.
    /// </summary>
    private static async Task<WorkflowRun> SeedRunAsync(
        WebOfWorkDbContext db,
        WorkflowRunStatus status,
        string stage = WorkflowStages.TalentMatch)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = $"owner-{Guid.NewGuid():N}@test.com",
            FullName = "Owner",
            PasswordHash = "x",
            Role = UserRole.Client,
            CurrentRole = UserRole.Client,
            Status = UserStatus.Active,
        };
        var profile = new ClientProfile { Id = Guid.NewGuid(), UserId = user.Id, User = user, CompanyName = "Acme" };
        var project = new Project
        {
            ClientId = profile.Id,
            Client = profile,
            Title = "Need a landing page",
            Description = "A description long enough to be valid.",
            PricingType = ProjectPricingType.Fixed,
            FixedPrice = 1000m,
            ExpectedDays = 10,
        };

        db.Users.Add(user);
        db.ClientProfiles.Add(profile);
        db.Projects.Add(project);

        var run = new WorkflowRun
        {
            Objective = "Find talent",
            Stage = stage,
            Status = status,
            ProjectId = project.Id,
            RequestedByUserId = user.Id,
            RequestedByUser = user,
        };
        db.WorkflowRuns.Add(run);
        await db.SaveChangesAsync();
        return run;
    }
}
