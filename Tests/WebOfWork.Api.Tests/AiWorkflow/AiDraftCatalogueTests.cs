using System.Reflection;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using WebOfWork.Api.Controllers;
using WebOfWork.Api.Data;

using WebOfWork.Api.Models;
using WebOfWork.Api.Services;
using Xunit;

namespace WebOfWork.Api.Tests.AiWorkflow;

/// <summary>
/// The catalogue the draft agent is given.
/// </summary>
/// <remarks>
/// <para>
/// <c>POST /api/ai/draft</c> hands the agent a "closed vocabulary" so it grounds
/// its <c>category_id</c> and <c>skill_ids</c> instead of inventing them. That
/// only works if the vocabulary actually contains the skills.
/// </para>
/// <para>
/// It did not. The projection was <c>new { c.Id, c.Name }</c>, so the agent
/// received seven categories and no skills at all. The Python grounding pass
/// could not tell a category row from a flat skill row -- both are
/// <c>{id, name}</c> -- so it promoted all seven categories into the skill pool
/// and reported a live draft's "skills" as <c>["Development", "Design"]</c>,
/// while the database held 654 real skills (HTML5, CSS3, Tailwind CSS,
/// Responsive Design) that the agent had never been shown.
/// </para>
/// <para>
/// The drafter could not have picked correctly. It was never told what correct
/// looked like, and the grounding check that should have caught this was
/// comparing categories against categories.
/// </para>
/// </remarks>
public class AiDraftCatalogueTests
{
    private sealed class CapturingWorkflows : IAiWorkflowService
    {
        public object? CapturedContext { get; private set; }

        public Task<WorkflowRun> StartWorkflowAsync(
            Guid requestedByUserId, string objective, string stage, string? plan = null,
            int? projectId = null, object? context = null, CancellationToken ct = default,
            bool dispatchToAgent = true)
        {
            CapturedContext = context;
            return Task.FromResult(new WorkflowRun
            {
                Objective = objective,
                Stage = stage,
                RequestedByUserId = requestedByUserId,
                Status = WorkflowRunStatus.Pending,
            });
        }

        public Task DispatchAsync(int workflowRunId, object? context = null, CancellationToken ct = default) => Task.CompletedTask;

        public Task<WorkflowRun> StartWorkflowDeferredAsync(
            Guid requestedByUserId, string objective, string stage, int? projectId = null,
            object? context = null, CancellationToken ct = default)
        {
            CapturedContext = context;
            return StartWorkflowAsync(requestedByUserId, objective, stage, null, projectId, context, ct);
        }
        public Task<WorkflowRun> ResumeWorkflowAsync(int id, Guid u, string v, CancellationToken c = default) => throw new NotSupportedException();
        public Task<ShortlistApprovalResult> ApproveShortlistAsync(int id, Guid u, IReadOnlyCollection<Guid> ids, string? r, IReadOnlyDictionary<Guid, Guid>? p = null, CancellationToken c = default) => throw new NotSupportedException();
        public Task<WorkflowRun> ApproveWorkflowAsync(int id, Guid u, string? d = null, string? r = null, WorkflowContractSpec? s = null, CancellationToken c = default) => throw new NotSupportedException();
        public Task<WorkflowRun> CancelWorkflowAsync(int id, Guid u, CancellationToken c = default) => throw new NotSupportedException();
        public Task<WorkflowStepLog> AppendStepLogAsync(int id, string s, string? a, string? i, string? o, string st, long d, CancellationToken c = default) => throw new NotSupportedException();
        public Task<ToolCallLog> AppendToolCallLogAsync(int id, string t, string? i, string? o, bool s, long d, CancellationToken c = default) => throw new NotSupportedException();
        public Task<AgentRecommendation> AppendAgentRecommendationAsync(int id, AgentRecommendation r, CancellationToken c = default) => throw new NotSupportedException();
        public Task<ProposalScreening> AppendProposalScreeningAsync(int id, ProposalScreening s, CancellationToken c = default) => throw new NotSupportedException();
        public Task<ValidationResult> AppendValidationResultAsync(int id, ValidationResult v, CancellationToken c = default) => throw new NotSupportedException();
        public Task<WorkflowRun> CompleteWorkflowAsync(int id, string? o, string? status = null, CancellationToken c = default) => throw new NotSupportedException();
    }

    /// <summary>
    /// The controller builds its context as an anonymous type, so the test reads
    /// it by reflection rather than by a cast that would need the type to be
    /// public.
    /// </summary>
    private static List<Dictionary<string, object?>> ReadCategories(object? context)
    {
        context.Should().NotBeNull("the agent is given its vocabulary in the context");

        var categories = context!.GetType().GetProperty("categories")?.GetValue(context);
        categories.Should().BeAssignableTo<IEnumerable<object>>(
            "the context carries the catalogue as `categories`");

        var rows = new List<Dictionary<string, object?>>();
        foreach (var row in (IEnumerable<object>)categories!)
        {
            var type = row.GetType();
            var id = type.GetProperty("Id")!.GetValue(row);
            var name = type.GetProperty("Name")!.GetValue(row);
            var skills = type.GetProperty("Skills")?.GetValue(row) as IEnumerable<object>;

            var skillRows = new List<(int Id, string Name)>();
            foreach (var skill in skills ?? Array.Empty<object>())
            {
                var skillType = skill.GetType();
                skillRows.Add((
                    (int)skillType.GetProperty("Id")!.GetValue(skill)!,
                    (string)skillType.GetProperty("Name")!.GetValue(skill)!));
            }

            rows.Add(new Dictionary<string, object?>
            {
                ["Id"] = id,
                ["Name"] = name,
                ["Skills"] = skillRows,
            });
        }
        return rows;
    }

    private static async Task<List<Dictionary<string, object?>>> DraftAsync(string dbName)
    {
        var options = new DbContextOptionsBuilder<WebOfWorkDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        await using var db = new WebOfWorkDbContext(options);

        var development = new Category { Name = "Development" };
        var design = new Category { Name = "Design" };
        db.Categories.AddRange(development, design);
        // Saved first so the generated keys exist. Setting CategoryId from
        // development.Id before this point reads 0, which is a silent way to
        // write a test that asserts nothing.
        await db.SaveChangesAsync();

        db.Skills.AddRange(
            new Skill { Name = "HTML5", CategoryId = development.Id },
            new Skill { Name = "CSS3", CategoryId = development.Id },
            new Skill { Name = "Responsive Design", CategoryId = development.Id },
            new Skill { Name = "Figma", CategoryId = design.Id });
        await db.SaveChangesAsync();

        var workflows = new CapturingWorkflows();
        var currentUser = new Mock<ICurrentUser>();
        currentUser.Setup(x => x.Id).Returns(Guid.NewGuid());

        var controller = new AiDraftController(workflows, currentUser.Object, db);
        await controller.Draft(new DraftProjectRequest { PartialText = "A responsive marketing site." }, default);

        return ReadCategories(workflows.CapturedContext);
    }

    [Fact]
    public async Task The_draft_catalogue_carries_every_category()
    {
        var categories = await DraftAsync(nameof(The_draft_catalogue_carries_every_category));

        categories.Should().HaveCount(2);
        categories.Select(c => (string)c["Name"]!)
            .Should().BeEquivalentTo(new[] { "Development", "Design" });
    }

    /// <summary>
    /// The catalogue carries categories and NOT their skills.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This inverts an earlier decision, and the reason is worth keeping because
    /// the obvious-looking fix is the one that was wrong.
    /// </para>
    /// <para>
    /// The skills were nested because the server had been projecting
    /// <c>{Id, Name}</c> only, so the agent received seven categories and no
    /// skills, and its grounding "validated" category names as if they were
    /// skills -- a live draft came back with <c>skills: ["Development",
    /// "Design"]</c>.
    /// </para>
    /// <para>
    /// Nesting fixed that and created two worse problems. The payload carried all
    /// 654 skills on every draft, which pushed the conversation past the primary
    /// provider's request limit twice mid-draft ("413 Payload Too Large") and
    /// forced a provider fallback. And a 654-row list is worse to choose from
    /// than a 200-row one: the same brief then came back with ClickHouse,
    /// Snowflake and BigQuery, because the model was picking from everything at
    /// once rather than from the skills that apply.
    /// </para>
    /// <para>
    /// The agent has <c>list_skills_by_category</c> for the narrow lookup and
    /// does call it. One category's skills is a list a model can hold in mind,
    /// which is the property that was missing.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task The_draft_catalogue_does_not_bloat_with_every_skill()
    {
        var categories = await DraftAsync(nameof(The_draft_catalogue_does_not_bloat_with_every_skill));

        foreach (var category in categories)
        {
            // The fixture seeds three Development skills, so a nested payload
            // would come back non-empty. Empty is the assertion that actually
            // proves the projection is not nesting them.
            ((List<(int Id, string Name)>)category["Skills"]!).Should().BeEmpty(
                "skills come from list_skills_by_category; nesting them here put "
                + "all 654 rows on every draft and cost two provider fallbacks");
        }
    }

    [Fact]
    public async Task Every_category_is_present_even_when_it_has_no_skills()
    {
        // A category with no skills must still be listed: it is still a valid
        // category, and silently dropping it would make an empty category
        // unselectable.
        var options = new DbContextOptionsBuilder<WebOfWorkDbContext>()
            .UseInMemoryDatabase(nameof(Every_category_is_present_even_when_it_has_no_skills))
            .Options;
        await using var db = new WebOfWorkDbContext(options);
        db.Categories.Add(new Category { Name = "Empty" });
        await db.SaveChangesAsync();

        var workflows = new CapturingWorkflows();
        var currentUser = new Mock<ICurrentUser>();
        currentUser.Setup(x => x.Id).Returns(Guid.NewGuid());

        var controller = new AiDraftController(workflows, currentUser.Object, db);
        await controller.Draft(new DraftProjectRequest { PartialText = "Something." }, default);

        var categories = ReadCategories(workflows.CapturedContext);
        categories.Should().ContainSingle();
        ((string)categories[0]["Name"]!).Should().Be("Empty");
    }
}