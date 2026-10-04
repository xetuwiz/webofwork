using System.Text.Json;
using FluentAssertions;
using WebOfWork.Api.Controllers;
using WebOfWork.Api.Services;
using Xunit;

namespace WebOfWork.Api.Tests.AiWorkflow;

/// <summary>
/// A client's chosen scoring weights.
/// </summary>
/// <remarks>
/// This is the contract behind the "adjust the weights and re-run" control. The
/// test that matters is not that a valid plan is accepted -- it is the pair of
/// failure modes that would make the control a lie:
///
/// <list type="bullet">
/// <item>a plan whose weights do not sum to 1 is <em>rejected</em>, because the
/// weights are relative and a total of 1.3 over-scores every candidate uniformly,
/// which looks like a stronger shortlist rather than a bug;</item>
/// <item>a plan that is rejected here is <em>also</em> rejected in the agent
/// service, so a second caller that skips this controller still cannot get a
/// shortlist scored on numbers it did not choose.</item>
/// </list>
/// </remarks>
public class MatchPlanDtoTests
{
    private static MatchPlanDto Valid() => new()
    {
        SkillCoverage = 0.5,
        PortfolioRelevance = 0.2,
        PastSuccessRate = 0.1,
        AvailabilityAndSpeed = 0.1,
        BudgetFit = 0.1,
        ShortlistSize = 10,
    };

    [Fact]
    public void Defaults_AreAValidPlan()
    {
        // The values a client sees before touching anything must themselves be
        // acceptable, or the control starts in a state the server refuses.
        new MatchPlanDto().Validate().Should().BeNull();
    }

    [Fact]
    public void AValidPlan_IsAccepted()
    {
        Valid().Validate().Should().BeNull();
    }

    [Fact]
    public void WeightsThatDoNotSumToOne_AreRejected()
    {
        var plan = Valid();
        plan.SkillCoverage = 0.9;

        plan.Validate().Should().NotBeNull()
            .And.Contain("sum to");
    }

    [Theory]
    [InlineData(-1.0, 0.5)]
    [InlineData(1.5, -0.5)]
    public void AWeightOutsideZeroToOne_IsRejected_AndTheNameIsGiven(double bad, double compensate)
    {
        // `{-1, 0, 0, 0, 2}` sums to 1 and inverts the score, so the sum check
        // alone would pass it. Each weight needs its own bound.
        var plan = new MatchPlanDto
        {
            SkillCoverage = bad,
            PortfolioRelevance = compensate,
            PastSuccessRate = 0.0,
            AvailabilityAndSpeed = 0.0,
            BudgetFit = 0.0,
        };

        var problem = plan.Validate();

        problem.Should().NotBeNull();
        problem!.Should().Match(p => p.Contains("between 0 and 1"));
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void AWeightThatIsNotAFiniteNumber_IsRejected(double value)
    {
        // NaN fails every comparison, so a naive range check would let it through
        // and the weights would sum to NaN -- which compares false against 1.0
        // and so would also pass a sum check. Both checks have to be explicit.
        var plan = Valid();
        plan.SkillCoverage = value;

        plan.Validate().Should().NotBeNull();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    [InlineData(16)]
    [InlineData(100)]
    public void AShortlistSizeOutsideFiveToFifteen_IsRejected(int size)
    {
        var plan = Valid();
        plan.ShortlistSize = size;

        plan.Validate().Should().NotBeNull()
            .And.Contain("shortlist_size");
    }

    [Theory]
    [InlineData(5)]
    [InlineData(10)]
    [InlineData(15)]
    public void TheBoundaryShortlistSizes_AreAccepted(int size)
    {
        var plan = Valid();
        plan.ShortlistSize = size;

        plan.Validate().Should().BeNull();
    }

    [Fact]
    public void SerialisedWeights_AreSnakeCase()
    {
        // The agent service's Pydantic models are snake_case, and its
        // normalisation layer maps .NET camelCase to them. Emitting camelCase
        // here would produce a plan whose weights silently failed to bind, and
        // the run would fall back to defaults -- a successful run scored on
        // numbers nobody chose. This assertion is the guard for that.
        var json = JsonDocument.Parse(Valid().ToJson()).RootElement;

        var weights = json.GetProperty("weights");
        weights.GetProperty("skill_coverage").GetDouble().Should().Be(0.5);
        weights.GetProperty("portfolio_relevance").GetDouble().Should().Be(0.2);
        weights.GetProperty("past_success_rate").GetDouble().Should().Be(0.1);
        weights.GetProperty("availability_and_speed").GetDouble().Should().Be(0.1);
        weights.GetProperty("budget_fit").GetDouble().Should().Be(0.1);

        json.GetProperty("shortlist_size").GetInt32().Should().Be(10);

        // And nothing camelCase slipped through.
        json.GetProperty("weights").EnumerateObject()
            .Select(p => p.Name)
            .Should().OnlyContain(n => !n.Any(char.IsUpper));
    }

    [Fact]
    public void SerialisedPlan_OmitsTheFiltersRatherThanNullingThem()
    {
        // An explicit null for `hard_filters` is a value the agent service has to
        // decide how to interpret. Omitting it means its own sensible defaults
        // apply, which is one fewer thing to get wrong across a language boundary.
        var json = JsonDocument.Parse(Valid().ToJson()).RootElement;

        json.TryGetProperty("hard_filters", out _).Should().BeFalse(
            "omitting the key lets the agent service apply its own defaults");
    }

    [Fact]
    public void TheStoredPlanIsReadableBackAsJson()
    {
        // Round trip, because the controller returns the stored text to the
        // browser. A plan that cannot be read back would blank the weights
        // display while still being used for scoring.
        var stored = Valid().ToJson();

        using var reparsed = JsonDocument.Parse(stored);
        reparsed.RootElement.ValueKind.Should().Be(JsonValueKind.Object);
    }
}

/// <summary>
/// The plan the agent service reports back.
/// </summary>
public class AgentPlanExtractionTests
{
    [Fact]
    public void ThePlanIsExtractedFromASuccessfulResponse()
    {
        // Without this, a client that never touched a slider has no weights to
        // show and nothing to adjust -- the plan column is null on a first run
        // because the planner runs inside the agent service.
        var body = """
            {"status":"ok","stage":"talent_match","summary":"done",
             "payload":{"plan":{"weights":{"skill_coverage":0.35},
                                "shortlist_size":10},
                        "recommendations":[]}}
            """;

        using var document = JsonDocument.Parse(AiWorkflowService.ExtractPlanFromAgentResponse(body));
        document.RootElement.GetProperty("weights").GetProperty("skill_coverage").GetDouble()
            .Should().Be(0.35);
    }

    [Fact]
    public void NoPayloadYieldsNoPlan()
    {
        // A failure envelope. The run has already failed for its own reasons; the
        // weights display should show nothing rather than break.
        AiWorkflowService.ExtractPlanFromAgentResponse("""{"status":"failed","error":"boom"}""")
            .Should().BeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not json")]
    [InlineData("[1,2,3]")]
    [InlineData("""{"payload":"not an object"}""")]
    [InlineData("""{"payload":{"plan":"not an object"}}""")]
    [InlineData("""{"payload":{}}""")]
    public void AnythingUnusableYieldsNoPlan_AndNeverThrows(string? body)
    {
        // This runs on the success path of a run that has already succeeded, so
        // throwing would discard a good result over a cosmetic field.
        AiWorkflowService.ExtractPlanFromAgentResponse(body).Should().BeNull();
    }
}
