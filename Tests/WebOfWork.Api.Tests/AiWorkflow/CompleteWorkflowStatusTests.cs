using FluentAssertions;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Moq;
using WebOfWork.Api.Data;
using WebOfWork.Api.Models;
using WebOfWork.Api.Models.Enums;
using WebOfWork.Api.Services;
using Xunit;

namespace WebOfWork.Api.Tests.AiWorkflow;

/// <summary>
/// Where does a run land when a stage reports its output?
/// </summary>
/// <remarks>
/// <para>
/// <see cref="AiWorkflowService.CompleteWorkflowAsync"/> hardcoded
/// <c>WorkflowRunStatus.Completed</c>. That is wrong for the ordinary end of a
/// talent-match run, which stops by <i>waiting for a person</i>: Completed says
/// there is nothing left to do, and the status is what everything downstream
/// reads.
/// </para>
/// <para>
/// Two failures came out of it. <c>AiTalentMatchController.Start</c>
/// de-duplicates on <c>status IN (0, 1, 9)</c>, so a run parked at
/// <c>Pending</c> is handed back on every later press of "Find best talent"
/// instead of being re-dispatched. And <c>ShortlistApprovalPanel</c> enables
/// itself only for <c>PendingClientApproval</c>, so a Completed run can never be
/// approved. Observed live on run 24.
/// </para>
/// <para>
/// The status arrives on the DTO already -- <c>CompleteWorkflowStepRequest</c>
/// has carried a <c>Status</c> property, defaulted to "Completed", and the Python
/// client has always sent it. The controller simply dropped it.
/// </para>
/// </remarks>
public sealed class CompleteWorkflowStatusTests
{
    /// <summary>
    /// A fresh in-memory database, seeded with a client and one talent-match run.
    /// </summary>
    /// <param name="DatabaseName">
    /// The store's name, returned so a second context can be pointed at the same
    /// one. Taken from the value passed to <c>UseInMemoryDatabase</c> rather than
    /// read back off the context -- <c>GetDbConnection()</c> throws on a
    /// non-relational provider, which is how this test first failed.
    /// </param>
    private static async Task<(WebOfWorkDbContext Db, AiWorkflowService Service, WorkflowRun Run, string DatabaseName)>
        ScenarioAsync()
    {
        var databaseName = Guid.NewGuid().ToString();
        var db = new WebOfWorkDbContext(
            new DbContextOptionsBuilder<WebOfWorkDbContext>()
                .UseInMemoryDatabase(databaseName)
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

        var service = new AiWorkflowService(db, new Mock<IBackgroundJobClient>().Object);
        var run = await service.StartWorkflowAsync(
            client.Id, "Find talent", WorkflowStages.TalentMatch);

        return (db, service, run, databaseName);
    }

    private static WebOfWorkDbContext SecondContextFor(string databaseName)
        => new(new DbContextOptionsBuilder<WebOfWorkDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options);

    [Fact]
    public async Task No_status_leaves_the_run_Completed()
    {
        // The default, and the only status the method could previously produce.
        // Kept working so every existing caller is unaffected.
        var (db, service, run, _) = await ScenarioAsync();
        await using var _db = db;

        var updated = await service.CompleteWorkflowAsync(run.Id, """{"a":1}""");

        updated.Status.Should().Be(WorkflowRunStatus.Completed);
    }

    [Fact]
    public async Task A_run_waiting_on_a_person_lands_on_the_client_gate()
    {
        // The case that was broken. "Pending" here would make the start button a
        // no-op forever; "Completed" would leave the approve control dead.
        var (db, service, run, _) = await ScenarioAsync();
        await using var _db = db;

        var updated = await service.CompleteWorkflowAsync(
            run.Id, """{"plan":{"weights":{}}}""", "PendingClientApproval");

        updated.Status.Should().Be(WorkflowRunStatus.PendingClientApproval);
    }

    [Fact]
    public async Task The_change_is_actually_flushed_not_just_left_pending()
    {
        // Asserted through the change tracker rather than by re-reading.
        //
        // The obvious version of this test -- call the method, then read the row
        // back through a second context -- passes whether or not SaveChanges ever
        // runs. EF Core's in-memory provider stores object *references*, not
        // copies, so mutating the tracked entity mutates the instance sitting in
        // the store. Deleting the `await _db.SaveChangesAsync(ct)` line leaves
        // that test green. (It did: the mutation survived.)
        //
        // A pending-change entry is the honest signal. If the save is skipped the
        // entity is still `Modified` when the method returns.
        var (db, service, run, _) = await ScenarioAsync();
        await using var _db = db;

        await service.CompleteWorkflowAsync(run.Id, """{"plan":{}}""", "PendingClientApproval");

        var entry = db.ChangeTracker.Entries<WorkflowRun>()
            .SingleOrDefault(e => e.Entity.Id == run.Id);

        entry.Should().NotBeNull();
        entry!.State.Should().Be(
            EntityState.Unchanged,
            "SaveChanges was not called, so the new status was never written");
    }

    [Fact]
    public async Task The_status_is_visible_to_a_second_context()
    {
        // Worth keeping even though the InMemory provider makes it weaker than it
        // looks: it does catch a status that was never assigned to the tracked
        // entity at all, which is the more likely mistake. The flush itself is
        // pinned by the change-tracker test above, which is the honest one.
        var (db, service, run, databaseName) = await ScenarioAsync();

        await service.CompleteWorkflowAsync(run.Id, """{"plan":{}}""", "PendingClientApproval");

        // `db` is deliberately not disposed before the re-read: the in-memory
        // store is dropped once its last context goes.
        await using var reread = SecondContextFor(databaseName);

        var stored = await reread.WorkflowRuns.AsNoTracking().FirstAsync(x => x.Id == run.Id);
        stored.Status.Should().Be(WorkflowRunStatus.PendingClientApproval);
        stored.FinalOutput.Should().Contain("plan");
    }

    [Fact]
    public async Task The_output_is_still_written_alongside_the_status()
    {
        // These are the same call. A status change that lost the payload would
        // trade one silent failure for another.
        var (db, service, run, _) = await ScenarioAsync();
        await using var _db = db;

        var updated = await service.CompleteWorkflowAsync(
            run.Id, """{"plan_source":"planner"}""", "PendingClientApproval");

        updated.FinalOutput.Should().Contain("planner");
    }

    [Theory]
    [InlineData("PendingApproval")]
    [InlineData("PendingManagerApproval")]
    [InlineData("Failed")]
    [InlineData("Running")]
    public async Task Any_status_the_enum_knows_is_honoured(string status)
    {
        var (db, service, run, _) = await ScenarioAsync();
        await using var _db = db;

        var updated = await service.CompleteWorkflowAsync(run.Id, "{}", status);

        updated.Status.Should().Be(Enum.Parse<WorkflowRunStatus>(status));
    }

    [Theory]
    [InlineData("AwaitingApproval")]   // a plausible near-miss
    [InlineData("complete")]           // lower case, and it does parse
    [InlineData("")]
    [InlineData("   ")]
    public async Task An_unusable_status_costs_the_status_and_not_the_output(string status)
    {
        // The alternative -- throwing -- would refuse to record a good result
        // because of a label. The step log still carries the status either way.
        var (db, service, run, _) = await ScenarioAsync();
        await using var _db = db;

        var updated = await service.CompleteWorkflowAsync(run.Id, """{"kept":true}""", status);

        updated.FinalOutput.Should().Contain("kept");
        if (!Enum.TryParse<WorkflowRunStatus>(status, true, out var _parsed))
            updated.Status.Should().Be(WorkflowRunStatus.Completed);
    }

    [Fact]
    public async Task Status_matching_ignores_case()
    {
        // Python sends PascalCase today, but a shell script or curl would not,
        // and a case difference is not a reason to lose the gate.
        var (db, service, run, _) = await ScenarioAsync();
        await using var _db = db;

        var updated = await service.CompleteWorkflowAsync(
            run.Id, "{}", "pendingclientapproval");

        updated.Status.Should().Be(WorkflowRunStatus.PendingClientApproval);
    }
}