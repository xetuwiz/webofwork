using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using WebOfWork.Api.Controllers;
using WebOfWork.Api.Data;
using WebOfWork.Api.Helpers;
using WebOfWork.Api.Models;
using WebOfWork.Api.Models.Enums;
using WebOfWork.Api.Services;
using Xunit;

namespace WebOfWork.Api.Tests.AiWorkflow;

/// <summary>
/// Does the shortlist page show the weights the shortlist was actually produced
/// under?
/// </summary>
/// <remarks>
/// <para>
/// The failure this exists for. Run 23's planning step succeeded and wrote weights
/// (<c>skill_coverage 0.4</c>, <c>portfolio_relevance 0.25</c>, ...) into the step
/// log. The shortlist page rendered the panel's defaults and captioned them "The
/// AI has not chosen its weights yet. These are the defaults it starts from."
/// </para>
/// <para>
/// The weights were in the database the whole time, one column away. Two columns
/// hold them and the endpoint read only one:
///
/// <list type="bullet">
/// <item><c>WorkflowRun.Plan</c> — written only when the *client* supplied a plan,
/// i.e. a re-run from the sliders. Null on every ordinary first run.</item>
/// <item><c>WorkflowRun.FinalOutput</c> — written by <c>complete_workflow</c>, with
/// the planner's plan under a <c>plan</c> key.</item>
/// </list>
///
/// So <c>fromServer</c> was false on the client for every first run, and the panel
/// said the AI had chosen nothing. It had.
/// </para>
/// <para>
/// These assert the resolution rules, including which column wins. Asserting only
/// "a plan comes back" would pass with the planner's plan losing to a stale
/// supplied one, which is the opposite of correct.
/// </para>
/// </remarks>
public sealed class ShortlistPlanResolutionTests
{
    private const string PlannerWeights =
        """{"weights":{"skill_coverage":0.4,"portfolio_relevance":0.25},"shortlist_size":10}""";

    private static string PlannerOutput(string source = "planner") =>
        $$"""{"plan":{{PlannerWeights}},"plan_source":"{{source}}"}""";

    private static string SuppliedPlan(string weights = """{"skill_coverage":0.9}""") =>
        $$"""{"weights":{{weights}},"shortlist_size":3}""";

    private static string? Resolve(string? supplied, string? output)
        => AiTalentMatchController.ResolveShortlistPlan(supplied, output).Plan;

    private static string? SourceOf(string? supplied, string? output)
        => AiTalentMatchController.ResolveShortlistPlan(supplied, output).Source;

    public class ThePlannerChoseThese
    {
        [Fact]
        public void The_plan_comes_from_the_runs_output_when_no_plan_was_supplied()
        {
            // The live bug, in one assertion. `supplied` is null on a first run.
            Resolve(null, PlannerOutput()).Should().NotBeNull()
                .And.Contain("skill_coverage");
        }

        [Fact]
        public void The_source_is_reported_as_planner()
        {
            // Without this the panel cannot tell the client the AI chose these.
            SourceOf(null, PlannerOutput()).Should().Be("planner");
        }

        [Fact]
        public void The_weights_survive_intact()
        {
            using var document = JsonDocument.Parse(Resolve(null, PlannerOutput())!);
            document.RootElement.GetProperty("weights").GetProperty("skill_coverage")
                .GetDouble().Should().BeApproximately(0.4, 1e-9);
        }
    }

    public class AClientSetThese
    {
        [Fact]
        public void The_source_is_reported_as_supplied()
        {
            SourceOf(SuppliedPlan(), null).Should().Be("supplied");
        }

        [Fact]
        public void The_supplied_plan_is_returned()
        {
            Resolve(SuppliedPlan(), null).Should().Contain("skill_coverage");
        }
    }

    public class BothColumnsArePopulated
    {
        [Fact]
        public void The_supplied_plan_wins()
        {
            // The one the client actually reviewed, and the one the planner was
            // not allowed to overwrite on a re-run. Preferring the planner's here
            // would show weights the client did not approve.
            Resolve(SuppliedPlan(), PlannerOutput()).Should().Contain("0.9")
                .And.NotContain("0.4");
        }

        [Fact]
        public void And_it_is_labelled_supplied_not_planner()
        {
            // The label has to follow the plan, not just exist.
            SourceOf(SuppliedPlan(), PlannerOutput()).Should().Be("supplied");
        }
    }

    public class MalformedInputIsSteppedAside
    {
        [Fact]
        public void No_plan_anywhere_yields_null_rather_than_throwing()
        {
            Resolve(null, null).Should().BeNull();
        }

        [Fact]
        public void And_no_source_either()
        {
            // A null plan with a non-null source is a half-pair the client cannot
            // act on.
            SourceOf(null, null).Should().BeNull();
        }

        [Fact]
        public void Unparseable_output_yields_null()
        {
            Resolve(null, "{not json").Should().BeNull();
        }

        [Fact]
        public void Output_without_a_plan_key_yields_null()
        {
            // A completed draft run has an output with no `plan` in it.
            Resolve(null, """{"title":"x","skills":["css"]}""").Should().BeNull();
        }

        [Fact]
        public void A_non_object_plan_yields_null()
        {
            Resolve(null, """{"plan":"just a string"}""").Should().BeNull();
        }

        [Fact]
        public void A_non_object_output_yields_null()
        {
            Resolve(null, "[1,2,3]").Should().BeNull();
        }

        [Fact]
        public void An_unparseable_supplied_plan_falls_back_to_the_planners()
        {
            // Half-written jsonb is not a reason to show nothing.
            Resolve("{broken", PlannerOutput()).Should().Contain("skill_coverage");
        }

        [Fact]
        public void A_plan_with_no_source_is_returned_but_unlabelled()
        {
            // Better an honest "these are the weights this shortlist was produced
            // with" than a guessed attribution.
            var plan = """{"plan":{"weights":{"skill_coverage":0.4}}}""";
            Resolve(null, plan).Should().NotBeNull();
            SourceOf(null, plan).Should().BeNull();
        }

        [Fact]
        public void A_non_string_source_is_treated_as_no_source()
        {
            // The other half of the guard, and the only one a mutation that
            // substitutes a literal for `s.GetString()` can actually reach: when
            // the key is *absent* `TryGetProperty` is false and the ternary yields
            // null either way, so testing absence alone cannot tell a correct
            // reader from a guessed one.
            var output = """{"plan":{"weights":{"skill_coverage":0.4}},"plan_source":7}""";
            SourceOf(null, output).Should().BeNull();
        }

        [Fact]
        public void And_the_plan_beside_it_is_still_returned()
        {
            // An unreadable label must not cost the client the weights.
            var output = """{"plan":{"weights":{"skill_coverage":0.4}},"plan_source":{}}""";
            Resolve(null, output).Should().Contain("skill_coverage");
            SourceOf(null, output).Should().BeNull();
        }

        [Fact]
        public void A_source_written_by_something_else_is_passed_through_verbatim()
        {
            // Not coerced to "planner". A future writer that labels its plans
            // differently should reach the client unchanged, and the panel's
            // fallback caption is the honest one for a label we do not recognise.
            var output = """{"plan":{"weights":{"skill_coverage":0.4}},"plan_source":"human_review"}""";
            SourceOf(null, output).Should().Be("human_review");
        }
    }

    /// <summary>
    /// The same rules, through <c>Shortlist</c> itself.
    /// </summary>
    /// <remarks>
    /// The unit tests above pin the resolution. These pin that the endpoint
    /// actually *calls* it, which is the other half: a helper that is correct and
    /// never invoked produces exactly the live bug, and nothing above would notice.
    /// </remarks>
    public class ThroughTheEndpoint
    {
        [Fact]
        public async Task A_first_run_returns_the_planners_weights_and_says_so()
        {
            var body = await GetShortlistBodyAsync(run =>
            {
                run.Plan = null;
                run.FinalOutput = PlannerOutput();
            });

            var plan = ReadString(body, "plan");
            plan.Should().NotBeNull().And.Contain("skill_coverage");
            ReadString(body, "plan_source").Should().Be("planner");
        }

        [Fact]
        public async Task A_rerun_returns_the_supplied_plan_and_says_so()
        {
            var body = await GetShortlistBodyAsync(run =>
            {
                run.Plan = SuppliedPlan();
                run.FinalOutput = PlannerOutput();
            });

            ReadString(body, "plan").Should().Contain("0.9").And.NotContain("0.4");
            ReadString(body, "plan_source").Should().Be("supplied");
        }

        [Fact]
        public async Task The_shortlist_itself_is_unaffected_by_a_plan_that_will_not_parse()
        {
            // The weights are explanatory. A broken plan must not cost the client
            // the answer they came for.
            var body = await GetShortlistBodyAsync(run =>
            {
                run.Plan = null;
                run.FinalOutput = "{broken";
            }, withRecommendation: true);

            ReadString(body, "plan").Should().BeNull();
            ReadArray(body, "shortlist").Should().HaveCount(1);
        }

        /// <summary>
        /// Does the client learn who each candidate is?
        /// </summary>
        /// <remarks>
        /// The shortlist endpoint projects recommendations on their own, which
        /// carry a profile id and a score and nothing else. The React panel then
        /// rendered a hardcoded literal "Freelancer" for every row, because there
        /// was no name on the wire to render.
        ///
        /// The join is deliberately <c>freelancer_profiles</c> to <c>users</c>,
        /// not straight to <c>users</c>: <c>AgentRecommendation.FreelancerId</c>
        /// is the profile id, so joining <c>users</c> on it directly matches zero
        /// rows and yields null with no error anywhere -- which is exactly how four
        /// nameless candidates shipped.
        /// </remarks>
        [Fact]
        public async Task Each_candidate_arrives_with_a_name_the_client_can_render()
        {
            var body = await GetShortlistBodyAsync(run => { }, withRecommendation: true);

            var row = ReadArray(body, "shortlist").Single();

            ReadString(row, "freelancer_name").Should().Be("Candidate");
            ReadString(row, "tagline").Should().Be("Full-stack developer");
        }

        /// <summary>
        /// A recommendation whose profile has gone should not come back as a
        /// nameless row.
        /// </summary>
        /// <remarks>
        /// The candidate cannot be invited without a profile -- the approval
        /// endpoint resolves ids as freelancer profiles -- so a row for them would
        /// be a dead end on the one screen that exists to act.
        /// </remarks>
        [Fact]
        public async Task A_candidate_whose_profile_is_gone_drops_out_rather_than_going_nameless()
        {
            await using var db = new WebOfWorkDbContext(
                new DbContextOptionsBuilder<WebOfWorkDbContext>()
                    .UseInMemoryDatabase(Guid.NewGuid().ToString())
                    .Options);

            var owner = new User
            {
                Email = $"{Guid.NewGuid()}@example.com",
                FullName = "Owner",
                PasswordHash = "hash",
                Role = UserRole.Client,
                Status = UserStatus.Active,
                IsEmailVerified = true,
            };
            db.Users.Add(owner);
            await db.SaveChangesAsync();

            var run = new WorkflowRun
            {
                ProjectId = 1,
                RequestedByUserId = owner.Id,
                Stage = WorkflowStages.TalentMatch,
                Objective = "Find talent",
                Status = WorkflowRunStatus.PendingClientApproval,
            };
            db.WorkflowRuns.Add(run);
            await db.SaveChangesAsync();

            // A profile id with nothing behind it: a deleted profile, or a
            // recommendation written before the profile existed.
            db.AgentRecommendations.Add(new AgentRecommendation
            {
                ProjectId = 1,
                WorkflowRunId = run.Id,
                FreelancerId = Guid.NewGuid(),
                Score = 0.9m,
                Reason = "Excellent fit",
            });
            await db.SaveChangesAsync();

            var currentUser = new Mock<ICurrentUser>();
            currentUser.Setup(c => c.Id).Returns(owner.Id);

            var controller = new AiTalentMatchController(
                new Mock<IAiWorkflowService>().Object,
                currentUser.Object,
                db,
                new Mock<IStageResolver>().Object);

            var result = await controller.Shortlist(run.Id, CancellationToken.None);
            var ok = result.Should().BeOfType<OkObjectResult>().Subject;

            ReadArray(ok.Value!, "shortlist").Should().BeEmpty();
        }

        private static string? ReadString(object body, string name)
            => ((IReadOnlyDictionary<string, object?>)ToMap(body))[name] as string;

        /// <summary>
        /// Reads a projected sequence off the anonymous response object.
        /// </summary>
        /// <remarks>
        /// Cast to <see cref="IEnumerable"/> rather than <c>object[]</c>. The
        /// projection is a deferred <c>IEnumerable</c> of anonymous types, and a
        /// direct <c>object[]</c> cast returns null -- which this helper would
        /// have turned into an empty array, reporting "no shortlist" for a run
        /// that had one.
        /// </remarks>
        private static IReadOnlyCollection<object> ReadArray(object body, string name)
        {
            var value = ((IReadOnlyDictionary<string, object?>)ToMap(body))[name];
            return value is System.Collections.IEnumerable sequence and not string
                ? sequence.Cast<object>().ToList()
                : Array.Empty<object>();
        }

        private static Dictionary<string, object?> ToMap(object body)
        {
            var map = new Dictionary<string, object?>();
            var properties = body.GetType().GetProperties();
            foreach (var property in properties)
                map[property.Name] = property.GetValue(body);
            return map;
        }

        private static async Task<object> GetShortlistBodyAsync(
            Action<WorkflowRun> configure,
            bool withRecommendation = false)
        {
            await using var db = new WebOfWorkDbContext(
                new DbContextOptionsBuilder<WebOfWorkDbContext>()
                    .UseInMemoryDatabase(Guid.NewGuid().ToString())
                    .Options);

            var owner = new User
            {
                Email = $"{Guid.NewGuid()}@example.com",
                FullName = "Owner",
                PasswordHash = "hash",
                Role = UserRole.Client,
                Status = UserStatus.Active,
                IsEmailVerified = true,
            };
            db.Users.Add(owner);
            await db.SaveChangesAsync();

            var run = new WorkflowRun
            {
                ProjectId = 1,
                RequestedByUserId = owner.Id,
                Stage = WorkflowStages.TalentMatch,
                Objective = "Find talent",
                Status = WorkflowRunStatus.PendingClientApproval,
            };
            configure(run);
            db.WorkflowRuns.Add(run);
            // Saved here so the run has its generated id, which the
            // recommendation below needs as a foreign key.
            await db.SaveChangesAsync();

            if (withRecommendation)
            {
                // A real freelancer, not a bare Guid.
                //
                // `Shortlist` joins recommendations to `freelancer_profiles` to
                // `users` so the client can name the candidate. That join is
                // inner, so a recommendation pointing at a profile that does not
                // exist drops out of the shortlist entirely -- and this test,
                // which asserts the shortlist survives a plan that will not parse,
                // would then pass for the wrong reason: an empty shortlist looks
                // the same as a healthy one when the only assertion is the plan.
                //
                // `FreelancerId` is the **profile** id, not the user id.
                var freelancer = new User
                {
                    Email = $"{Guid.NewGuid()}@example.com",
                    FullName = "Candidate",
                    PasswordHash = "hash",
                    Role = UserRole.Freelancer,
                    Status = UserStatus.Active,
                    IsEmailVerified = true,
                };
                db.Users.Add(freelancer);
                await db.SaveChangesAsync();

                db.FreelancerProfiles.Add(new FreelancerProfile
                {
                    UserId = freelancer.Id,
                    Tagline = "Full-stack developer",
                });
                await db.SaveChangesAsync();

                var profileId = db.FreelancerProfiles
                    .Single(x => x.UserId == freelancer.Id).Id;

                db.AgentRecommendations.Add(new AgentRecommendation
                {
                    ProjectId = 1,
                    // Set after the run has an id, rather than via the navigation:
                    // the in-memory provider does not fix up a navigation on a
                    // graph-disconnected entity, and a silently absent
                    // recommendation would make this test pass for the wrong
                    // reason.
                    WorkflowRunId = run.Id,
                    FreelancerId = profileId,
                    Score = 0.8m,
                    Reason = "Decent fit",
                });
            }

            await db.SaveChangesAsync();

            var currentUser = new Mock<ICurrentUser>();
            currentUser.Setup(c => c.Id).Returns(owner.Id);

            var controller = new AiTalentMatchController(
                new Mock<IAiWorkflowService>().Object,
                currentUser.Object,
                db,
                new Mock<IStageResolver>().Object);

            var result = await controller.Shortlist(run.Id, CancellationToken.None);

            var ok = result.Should().BeOfType<OkObjectResult>().Subject;
            return ok.Value!;
        }
    }
}