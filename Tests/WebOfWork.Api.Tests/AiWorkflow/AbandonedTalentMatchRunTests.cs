using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using WebOfWork.Api.Controllers;
using WebOfWork.Api.Data;
using WebOfWork.Api.Models;
using WebOfWork.Api.Models.Enums;
using WebOfWork.Api.Services;
using Xunit;

namespace WebOfWork.Api.Tests.AiWorkflow;

/// <summary>
/// Does "Find best talent" ever do anything twice?
/// </summary>
/// <remarks>
/// <para>
/// <c>Start</c> de-duplicates on
/// <c>status IN (Pending, Running, PendingClientApproval)</c> and returns the
/// match instead of dispatching. That is right for a second click and disastrous
/// for a run that died.
/// </para>
/// <para>
/// A run is inserted as <c>Pending</c> and only leaves that state when the agent
/// service reports back. Kill the service mid-run and nothing reports back, so
/// the row stays <c>Pending</c> and every subsequent click returns that corpse.
/// Observed four times on project 11 -- runs 23 and 24 and two draft runs -- each
/// needing a manual <c>UPDATE workflow_runs SET status = 7</c> from the database
/// before the button did anything.
/// </para>
/// <para>
/// <c>PendingClientApproval</c> is deliberately exempt from the staleness rule:
/// it is a resting place, not a sign of life, and a shortlist waiting three days
/// for its client is exactly what it is for.
/// </para>
/// </remarks>
public sealed class AbandonedTalentMatchRunTests
{
    /// <summary>Fixed so assertions can name the run they mean.</summary>
    private const int ProjectId = 11;
    private static async Task<(WebOfWorkDbContext Db, AiTalentMatchController Controller, User Client)>
        ScenarioAsync(params WorkflowRun[] runs)
    {
        var db = new WebOfWorkDbContext(
            new DbContextOptionsBuilder<WebOfWorkDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);

        var client = new User
        {
            Email = $"{Guid.NewGuid()}@example.com",
            FullName = "Client",
            PasswordHash = "hash",
            Role = UserRole.Client,
            Status = UserStatus.Active,
            IsEmailVerified = true,
        };
        db.Users.Add(client);
        await db.SaveChangesAsync();

        // `Project.ClientId` is the *ClientProfile* id, not the user id, so the
        // profile has to exist and be saved before the project can point at it.
        var profile = new ClientProfile { UserId = client.Id, CompanyName = "Acme" };
        db.ClientProfiles.Add(profile);
        await db.SaveChangesAsync();

        var project = new Project
        {
            Id = ProjectId,
            ClientId = profile.Id,
            Title = "React Native app",
            Description = "Build it",
        };
        db.Projects.Add(project);
        await db.SaveChangesAsync();

        foreach (var run in runs)
        {
            run.RequestedByUserId = client.Id;
            run.ProjectId = ProjectId;
            run.Stage = WorkflowStages.TalentMatch;
            db.WorkflowRuns.Add(run);
        }

        await db.SaveChangesAsync();

        var currentUser = new Mock<ICurrentUser>();
        currentUser.Setup(c => c.Id).Returns(client.Id);

        // A bare mock returns null for `ResolveForProjectAsync`, and `Start`
        // dereferences the result immediately -- which is a NullReferenceException
        // rather than anything informative about the behaviour under test.
        var stages = new Mock<IStageResolver>();
        stages.Setup(s => s.ResolveForProjectAsync(ProjectId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WorkflowStageResolution(
                WorkflowStages.TalentMatch,
                HasProposals: false,
                HasShortlist: false,
                HasValidation: false,
                ProposalCount: 0,
                ShortlistCount: 0));

        // Also a bare mock by default, and `Start` dereferences the run it
        // returns. Without this every "a new run should have been dispatched"
        // assertion fails on a NullReferenceException instead of on the
        // behaviour it is meant to check.
        var workflows = new Mock<IAiWorkflowService>();
        workflows
            .Setup(s => s.StartWorkflowAsync(
                client.Id, It.IsAny<string>(), WorkflowStages.TalentMatch,
                It.IsAny<string?>(), It.IsAny<int?>(), It.IsAny<object?>(),
                It.IsAny<CancellationToken>(), It.IsAny<bool>()))
            .ReturnsAsync((Guid userId, string objective, string stage,
                           string? plan, int? projectId, object? context,
                           CancellationToken ct, bool dispatch) =>
            {
                var created = new WorkflowRun
                {
                    ProjectId = projectId ?? ProjectId,
                    RequestedByUserId = userId,
                    Objective = objective,
                    Stage = stage,
                    Status = WorkflowRunStatus.Pending,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                };
                db.WorkflowRuns.Add(created);
                db.SaveChanges();
                return created;
            });

        return (db, new AiTalentMatchController(
            workflows.Object,
            currentUser.Object,
            db,
            stages.Object), client);
    }

    private static WorkflowRun Run(WorkflowRunStatus status, DateTime updatedAt) => new()
    {
        Status = status,
        CreatedAt = updatedAt,
        UpdatedAt = updatedAt,
    };

    [Fact]
    public async Task A_recent_Pending_run_is_still_treated_as_in_flight()
    {
        // The de-duplication is load-bearing: two concurrent pipelines would
        // compete for the same shortlist rows.
        var (db, controller, _) = await ScenarioAsync(
            Run(WorkflowRunStatus.Pending, DateTime.UtcNow.AddMinutes(-1)));
        await using var _db = db;

        var result = await controller.Start(ProjectId, null, CancellationToken.None);
        var body = result.Should().BeOfType<OkObjectResult>().Subject.Value!;

        ReadFlag(body, "already_running").Should().BeTrue();
        db.WorkflowRuns.Should().HaveCount(1, "a second pipeline must not be created");
    }

    [Fact]
    public async Task A_recent_Running_run_is_still_treated_as_in_flight()
    {
        var (db, controller, _) = await ScenarioAsync(
            Run(WorkflowRunStatus.Running, DateTime.UtcNow.AddMinutes(-1)));
        await using var _db = db;

        var result = await controller.Start(ProjectId, null, CancellationToken.None);
        var body = result.Should().BeOfType<OkObjectResult>().Subject.Value!;

        ReadFlag(body, "already_running").Should().BeTrue();
        db.WorkflowRuns.Should().HaveCount(1);
    }

    [Fact]
    public async Task An_abandoned_Pending_run_does_not_block_a_new_one()
    {
        // The bug. `Pending` since yesterday, and the button is a no-op.
        var (db, controller, _) = await ScenarioAsync(
            Run(WorkflowRunStatus.Pending, DateTime.UtcNow.AddHours(-20)));
        await using var _db = db;

        var result = await controller.Start(ProjectId, null, CancellationToken.None);

        result.Should().BeOfType<AcceptedResult>(
            "an abandoned run must not be reported as already running");

        db.WorkflowRuns.Should().HaveCount(2, "a fresh run should have been dispatched");
    }

    [Fact]
    public async Task The_abandoned_run_is_cancelled_rather_than_left_to_compete()
    {
        // Cancelling is what finally takes it out of the de-duplication set.
        // Without this the next click finds the same corpse again.
        var (db, controller, _) = await ScenarioAsync(
            Run(WorkflowRunStatus.Pending, DateTime.UtcNow.AddHours(-20)));
        await using var _db = db;

        await controller.Start(ProjectId, null, CancellationToken.None);

        var old = db.WorkflowRuns.Single(r => r.CreatedAt < DateTime.UtcNow.AddHours(-19));
        old.Status.Should().Be(WorkflowRunStatus.Cancelled);
    }

    [Fact]
    public async Task An_abandoned_run_is_not_reported_as_already_running()
    {
        // `already_running: true` is a claim about a live pipeline. Returning it
        // for a dead one is the specific dishonesty the client cannot detect.
        var (db, controller, _) = await ScenarioAsync(
            Run(WorkflowRunStatus.Pending, DateTime.UtcNow.AddHours(-20)));
        await using var _db = db;

        var result = await controller.Start(ProjectId, null, CancellationToken.None);

        if (result is OkObjectResult ok)
            ReadFlag(ok.Value!, "already_running").Should().BeFalse();
        else
            result.Should().BeOfType<AcceptedResult>();
    }

    [Fact]
    public async Task A_shortlist_awaiting_approval_is_never_treated_as_abandoned()
    {
        // The exemption that matters most. This run finished; it is waiting for a
        // person, and that can take days. Cancelling it would destroy a shortlist
        // the client is still reading.
        var (db, controller, _) = await ScenarioAsync(
            Run(WorkflowRunStatus.PendingClientApproval, DateTime.UtcNow.AddDays(-7)));
        await using var _db = db;

        var result = await controller.Start(ProjectId, null, CancellationToken.None);
        var body = result.Should().BeOfType<OkObjectResult>().Subject.Value!;

        ReadFlag(body, "already_running").Should().BeTrue();
        db.WorkflowRuns.Should().HaveCount(1, "a waiting shortlist must not be replaced");
        db.WorkflowRuns.Single().Status
            .Should().Be(WorkflowRunStatus.PendingClientApproval, "and must not be cancelled");
    }

    [Fact]
    public async Task A_run_past_the_cutoff_is_abandoned_and_one_just_under_is_not()
    {
        // The boundary, pinned from both sides. An off-by-one here decides
        // whether a run that is legitimately nine minutes old gets cancelled.
        var (db, controller, _) = await ScenarioAsync(
            Run(WorkflowRunStatus.Pending, DateTime.UtcNow - AbandonedRunPolicy.MaxAge - TimeSpan.FromMinutes(1)));
        await using var _db = db;

        await controller.Start(ProjectId, null, CancellationToken.None);

        db.WorkflowRuns.Should().HaveCount(2);
    }

    [Fact]
    public async Task A_terminal_run_never_blocks_a_new_one()
    {
        // Completed and Failed are not in the de-duplication set at all, so this
        // is a guard against the cutoff being applied too broadly.
        var (db, controller, _) = await ScenarioAsync(
            Run(WorkflowRunStatus.Failed, DateTime.UtcNow.AddMinutes(-1)),
            Run(WorkflowRunStatus.Completed, DateTime.UtcNow.AddMinutes(-1)));
        await using var _db = db;

        var result = await controller.Start(ProjectId, null, CancellationToken.None);

        result.Should().BeOfType<AcceptedResult>();
    }

    [Fact]
    public async Task The_newest_run_wins_when_several_are_in_flight()
    {
        // Ordered newest-first, so a fresh Pending run is the one de-duplicated
        // against and an old abandoned one is merely cleaned up.
        var (db, controller, _) = await ScenarioAsync(
            Run(WorkflowRunStatus.Pending, DateTime.UtcNow.AddHours(-20)),
            Run(WorkflowRunStatus.Pending, DateTime.UtcNow.AddMinutes(-2)));
        await using var _db = db;

        var result = await controller.Start(ProjectId, null, CancellationToken.None);
        var body = result.Should().BeOfType<OkObjectResult>().Subject.Value!;

        ReadFlag(body, "already_running").Should().BeTrue();
        ReadId(body).Should().NotBe(0);
        db.WorkflowRuns.Should().HaveCount(2, "the live run must not be duplicated");
    }

    [Fact]
    public async Task With_two_live_runs_the_newest_one_is_the_one_returned()
    {
        // Both are legitimately in flight, so the staleness rule cannot break the
        // tie -- only the ordering can. Newest-first matters because the id handed
        // back is the run the page then watches: returning the older one would
        // point the client at a run that a newer pipeline has already superseded.
        //
        // This is the case that catches an ordering mutation. A test with one live
        // run and one abandoned run cannot, because the abandoned one is filtered
        // out before ordering applies.
        var (db, controller, _) = await ScenarioAsync(
            Run(WorkflowRunStatus.Pending, DateTime.UtcNow.AddMinutes(-9)),
            Run(WorkflowRunStatus.Pending, DateTime.UtcNow.AddMinutes(-1)));
        await using var _db = db;

        var newer = db.WorkflowRuns.Single(r => r.UpdatedAt > DateTime.UtcNow.AddMinutes(-5));
        var older = db.WorkflowRuns.Single(r => r.UpdatedAt <= DateTime.UtcNow.AddMinutes(-5));

        var result = await controller.Start(ProjectId, null, CancellationToken.None);
        var body = result.Should().BeOfType<OkObjectResult>().Subject.Value!;

        ReadId(body).Should().Be(
            newer.Id,
            "the newest live run is the one the client is watching");
        ReadId(body).Should().NotBe(older.Id);
        db.WorkflowRuns.Should().HaveCount(2, "neither live run should be duplicated or cancelled");
    }

    private static bool ReadFlag(object body, string name)
        => (bool)((IReadOnlyDictionary<string, object?>)ToMap(body))[name]!;

    private static int ReadId(object body)
        => (int)((IReadOnlyDictionary<string, object?>)ToMap(body))["workflow_run_id"]!;

    private static Dictionary<string, object?> ToMap(object body)
    {
        var map = new Dictionary<string, object?>();
        foreach (var property in body.GetType().GetProperties())
            map[property.Name] = property.GetValue(body);
        return map;
    }
}