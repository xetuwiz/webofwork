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

/// <summary>
/// Approving an AI shortlist has to actually contact somebody.
///
/// ## The bug this file exists for
///
/// <c>ApproveShortlistAsync</c> built its invitation request with only a project
/// and a freelancer. An invitation is priced by its package -- there is no price
/// on an invitation of its own -- so the request carried <c>PackageId =
/// Guid.Empty</c>, <c>CreateInvitationAsync</c> rejected it with "Invalid package
/// ID", and every approved freelancer landed in <c>skipped</c>. With none
/// created, the run was marked <c>Failed</c> and the response still returned 200.
///
/// The worst combination: the client saw a successful approval, the run recorded
/// a failure, and nobody was contacted. The same omission was in
/// <c>InternalActionsController.SendInvitations</c>, so both routes to an
/// invitation were broken.
///
/// ## Why the test is at this level
///
/// It is not a controller test, because the controller only forwards. It is not a
/// unit test, because the defect was an id failing to survive a boundary between
/// two services -- and a mocked <c>IInvitationService</c> would have accepted the
/// malformed request happily, which is exactly how this survived for so long.
///
/// So the real <c>InvitationService</c> runs, against a real context, and the
/// assertion is on the rows that come out.
/// </summary>
public sealed class ShortlistApprovalCreatesInvitationsTests
{
    private static WebOfWorkDbContext InMemory(string name) =>
        new(new DbContextOptionsBuilder<WebOfWorkDbContext>()
            .UseInMemoryDatabase(name)
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options);

    /// <summary>
    /// A client, a project with a shortlist run on it, and freelancers who each
    /// have one package -- because a freelancer with no package cannot be invited
    /// at any price, which is a separate case with its own test below.
    /// </summary>
    private static async Task<Seed> ArrangeAsync(WebOfWorkDbContext db)
    {
        var clientUser = new User
        {
            Id = Guid.NewGuid(),
            Email = "client@test.com",
            FullName = "Client",
            PasswordHash = "x",
            Role = UserRole.Client,
            CurrentRole = UserRole.Client,
            Status = UserStatus.Active,
        };

        var clientProfile = new ClientProfile
        {
            Id = Guid.NewGuid(),
            User = clientUser,
            CompanyName = "Client Co",
            Location = "Colombo",
        };

        var project = new Project
        {
            ClientId = clientProfile.Id,
            Client = clientProfile,
            Title = "React build",
            Description = "A description long enough to satisfy validation.",
            Requirements = "Requirements",
            ExpectedDeliverables = "Deliverables",
            PricingType = ProjectPricingType.Fixed,
            FixedPrice = 2000m,
            ExpectedDays = 14,
            Status = ProjectStatus.Open,
            IsActive = true,
        };

        var freelancers = new List<(Guid ProfileId, Guid UserId)>();

        foreach (var index in new[] { 1, 2 })
        {
            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = $"freelancer{index}@test.com",
                FullName = $"Freelancer {index}",
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
                Phone = $"077000000{index}",
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
                PackageType = index == 1 ? "starter" : "pro",
                Price = index == 1 ? 500m : 1500m,
                DeliveryDays = 5,
                Description = "Package",
            });

            freelancers.Add((profile.Id, user.Id));
        }

        var run = new WorkflowRun
        {
            Objective = "find talent",
            Stage = "talent_match",
            Status = WorkflowRunStatus.PendingClientApproval,
            RequestedByUserId = clientUser.Id,
            ProjectId = 0,
        };

        db.Users.Add(clientUser);
        db.ClientProfiles.Add(clientProfile);
        db.Projects.Add(project);

        await db.SaveChangesAsync();

        // ProjectId is an identity, so the run can only be attached afterwards.
        run.ProjectId = project.Id;
        db.WorkflowRuns.Add(run);
        await db.SaveChangesAsync();

        return new Seed(clientUser.Id, clientProfile.Id, project.Id, run.Id, freelancers);
    }

    private sealed record Seed(
        Guid ClientUserId,
        Guid ClientProfileId,
        int ProjectId,
        int RunId,
        List<(Guid ProfileId, Guid UserId)> Freelancers);

    private static (AiWorkflowService Service, IInvitationService Invitations) Build(
        WebOfWorkDbContext db)
    {
        var invitations = new InvitationService(
            db,
            notificationPublisher: null,
            currentUser: null);

        var jobs = new Mock<IBackgroundJobClient>();
        jobs.Setup(j => j.Create(It.IsAny<Job>(), It.IsAny<IState>())).Returns("job-1");

        var service = new AiWorkflowService(db, jobs.Object, null, null, invitations);
        return (service, invitations);
    }

    [Fact]
    public async Task Approving_a_shortlist_creates_one_invitation_per_approved_freelancer()
    {
        await using var db = InMemory(nameof(Approving_a_shortlist_creates_one_invitation_per_approved_freelancer));
        var seed = await ArrangeAsync(db);
        var (service, _) = Build(db);

        var approved = seed.Freelancers.Select(f => f.ProfileId).ToList();

        var result = await service.ApproveShortlistAsync(
            seed.RunId, seed.ClientUserId, approved, "looks good");

        // The assertion the bug made impossible.
        result.InvitationIds.Should().HaveCount(2,
            "each approved freelancer has a package, so each can be priced and invited");

        result.Skipped.Should().BeEmpty("nobody should be skipped when everyone is invitable");

        var invitations = await db.Invitations.ToListAsync();
        invitations.Should().HaveCount(2);
        invitations.Should().OnlyContain(i => i.ProjectId == seed.ProjectId);
        invitations.Should().OnlyContain(i => i.Status == InvitationStatus.Pending);

        // And the run is not marked failed, because it did not fail.
        var run = await db.WorkflowRuns.FindAsync(seed.RunId);
        run!.Status.Should().NotBe(WorkflowRunStatus.Failed);
        run.ApprovalDecision.Should().Be("Shortlist Approved");
        run.ApprovedByUserId.Should().Be(seed.ClientUserId);
    }

    [Fact]
    public async Task Every_invitation_carries_a_real_package()
    {
        await using var db = InMemory(nameof(Every_invitation_carries_a_real_package));
        var seed = await ArrangeAsync(db);
        var (service, _) = Build(db);

        var approved = seed.Freelancers.Select(f => f.ProfileId).ToList();
        await service.ApproveShortlistAsync(seed.RunId, seed.ClientUserId, approved, null);

        var invitations = await db.Invitations.ToListAsync();
        invitations.Should().NotBeEmpty();

        // `Guid.Empty` here is the bug, restated: an invitation with no package
        // has no price, so the client's money moves against nothing.
        invitations.Should().OnlyContain(i => i.PackageId != Guid.Empty);

        var packageIds = await db.FreelancerPackages.Select(p => p.Id).ToListAsync();
        foreach (var invitation in invitations)
        {
            packageIds.Should().Contain(invitation.PackageId,
                "the package must be one that actually exists");
        }
    }

    [Fact]
    public async Task A_freelancer_with_no_package_is_skipped_with_a_reason_not_silently_dropped()
    {
        await using var db = InMemory(nameof(A_freelancer_with_no_package_is_skipped_with_a_reason_not_silently_dropped));
        var seed = await ArrangeAsync(db);
        var (service, _) = Build(db);

        // Remove one freelancer's package.
        var second = seed.Freelancers[1];
        db.FreelancerPackages.RemoveRange(
            db.FreelancerPackages.Where(p => p.UserId == second.UserId));
        await db.SaveChangesAsync();

        var result = await service.ApproveShortlistAsync(
            seed.RunId, seed.ClientUserId, seed.Freelancers.Select(f => f.ProfileId).ToList(), null);

        // One succeeds, one is reported. The client is told, rather than being
        // left to believe both were contacted.
        result.InvitationIds.Should().HaveCount(1);
        result.Skipped.Should().HaveCount(1);
        result.Skipped[0].FreelancerId.Should().Be(second.ProfileId);
        result.Skipped[0].Reason.Should().Contain("package");
    }

    [Fact]
    public async Task A_partial_approval_invites_only_those_named()
    {
        await using var db = InMemory(nameof(A_partial_approval_invites_only_those_named));
        var seed = await ArrangeAsync(db);
        var (service, _) = Build(db);

        // The client deselects one of the two the shortlist offered. That is the
        // override that matters: the shortlist is a suggestion.
        var chosen = seed.Freelancers[0].ProfileId;

        var result = await service.ApproveShortlistAsync(
            seed.RunId, seed.ClientUserId, new[] { chosen }, "only this one");

        result.InvitationIds.Should().HaveCount(1);

        var invitations = await db.Invitations.ToListAsync();
        invitations.Should().HaveCount(1);
        invitations[0].FreelancerId.Should().Be(chosen);
    }

    [Fact]
    public async Task Approving_nobody_is_refused()
    {
        await using var db = InMemory(nameof(Approving_nobody_is_refused));
        var seed = await ArrangeAsync(db);
        var (service, _) = Build(db);

        // An empty approval would end the run without contacting anyone, which is
        // not a decision the client can make by clicking Approve on nothing.
        var act = () => service.ApproveShortlistAsync(seed.RunId, seed.ClientUserId, Array.Empty<Guid>(), null);

        await act.Should().ThrowAsync<ArgumentException>();

        (await db.Invitations.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Somebody_else_cannot_approve_a_shortlist()
    {
        await using var db = InMemory(nameof(Somebody_else_cannot_approve_a_shortlist));
        var seed = await ArrangeAsync(db);
        var (service, _) = Build(db);

        var stranger = Guid.NewGuid();
        var approved = seed.Freelancers.Select(f => f.ProfileId).ToList();

        var act = () => service.ApproveShortlistAsync(seed.RunId, stranger, approved, null);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();

        // And nothing was contacted on the way out.
        (await db.Invitations.CountAsync()).Should().Be(0,
            "a rejected approval must not leave invitations behind");
    }

    [Fact]
    public async Task A_freelancer_who_already_proposed_is_skipped_rather_than_failing_the_whole_approval()
    {
        await using var db = InMemory(nameof(A_freelancer_who_already_proposed_is_skipped_rather_than_failing_the_whole_approval));
        var seed = await ArrangeAsync(db);
        var (service, _) = Build(db);

        // A shortlist assembled from people who applied will contain applicants.
        // Failing the entire approval for one of them would mean a client who
        // approved ten people gets no invitations at all.
        var applicant = seed.Freelancers[1];
        // The *profile* id, which is what `Proposal.FreelancerId` is a foreign
        // key to. Using the user id here looked plausible and silently produced a
        // proposal the guard does not match -- which is the same class of mistake
        // as the bug this file covers, and a reminder that the id to use is not
        // obvious from the variable name.
        db.Proposals.Add(new Proposal
        {
            ProjectId = seed.ProjectId,
            FreelancerId = applicant.ProfileId,
            CoverLetter = "Already applied",
            BidAmount = 1200m,
            DeliveryDays = 7,
            Status = ProposalStatus.Pending,
        });
        await db.SaveChangesAsync();

        var approved = seed.Freelancers.Select(f => f.ProfileId).ToList();
        var result = await service.ApproveShortlistAsync(seed.RunId, seed.ClientUserId, approved, null);

        result.InvitationIds.Should().HaveCount(1, "the other freelancer is still reachable");
        result.Skipped.Should().HaveCount(1, "and the one who already applied is reported");
        result.Skipped[0].Reason.ToLowerInvariant().Should().Contain("proposal");
    }
}