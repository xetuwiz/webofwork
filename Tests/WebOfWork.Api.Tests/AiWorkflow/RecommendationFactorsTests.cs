using System.Text.Json.Nodes;
using WebOfWork.Api.Helpers;
using Xunit;

namespace WebOfWork.Api.Tests.AiWorkflow;

/// <summary>
/// The factors document is written by Python and read by C#.
/// </summary>
/// <remarks>
/// Nothing at the boundary enforces that the two agree: the column is a JSON
/// blob, so a renamed key is not a compile error, it is a screen that quietly
/// stops showing evidence behind a score. These tests pin the agreement.
/// </remarks>
public class RecommendationFactorsTests
{
    /// <summary>
    /// The merge the agent write path relies on: factors in, factors plus the
    /// two lists out, with the numeric factors intact.
    /// </summary>
    [Fact]
    public void Merge_preserves_the_numeric_factors_and_adds_the_lists()
    {
        const string factors = """
            {"skill_coverage":0.9,"portfolio_relevance":0.75,"past_success_rate":1.0,
             "availability_and_speed":0.6,"budget_fit":0.85}
            """;

        var merged = RecommendationFactors.Merge(
            factors, new List<string> { "React", "Stripe" }, new List<string> { "C#" });

        var node = JsonNode.Parse(merged!)!.AsObject();

        // Untouched -- these are what the score is made of.
        Assert.Equal(0.9m, node["skill_coverage"]!.GetValue<decimal>());
        Assert.Equal(1.0m, node["past_success_rate"]!.GetValue<decimal>());

        Assert.Equal(new[] { "React", "Stripe" },
            RecommendationFactors.ReadStrengths(merged));
        Assert.Equal(new[] { "C#" }, RecommendationFactors.ReadGaps(merged));
    }

    /// <summary>
    /// The bug this helper exists to fix: the agent has always sent strengths
    /// and gaps, the DTO had nowhere to put them, and they were dropped.
    /// </summary>
    [Fact]
    public void Merge_keeps_the_lists_that_were_previously_discarded()
    {
        var merged = RecommendationFactors.Merge(
            """{"skill_coverage":0.5}""", new List<string> { "Node" }, new List<string>());

        Assert.Single(RecommendationFactors.ReadStrengths(merged));
        // Gaps empty means "not supplied", not "there are none", so nothing is
        // written -- a client must not be shown an empty list as though it were
        // an assessment.
        Assert.Empty(RecommendationFactors.ReadGaps(merged));
    }

    [Fact]
    public void Merge_with_nothing_to_add_leaves_the_document_untouched()
    {
        const string factors = """{"skill_coverage":0.4}""";

        Assert.Equal(factors, RecommendationFactors.Merge(factors, null, new List<string>()));
        Assert.Equal(factors, RecommendationFactors.Merge(factors, new List<string>(), null));
    }

    /// <summary>
    /// A null factors column and empty lists must produce null, not "{}".
    /// </summary>
    /// <remarks>
    /// "{}" would be indistinguishable from "scored, with no factors", which is
    /// a different claim from "not scored".
    /// </remarks>
    [Fact]
    public void Merge_with_nothing_at_all_stays_null()
    {
        Assert.Null(RecommendationFactors.Merge(null, null, null));
        Assert.Null(RecommendationFactors.Merge("", new List<string>(), new List<string>()));
    }

    /// <summary>
    /// A malformed blob costs a reader its factor breakdown, not the invitation.
    /// </summary>
    [Fact]
    public void Merge_tolerates_a_factors_column_that_is_not_json()
    {
        var merged = RecommendationFactors.Merge(
            "not json at all", new List<string> { "React" }, null);

        // The lists survive, because there is nowhere in a non-object to put
        // them and keeping the string would guarantee they are lost.
        Assert.Equal(new[] { "React" }, RecommendationFactors.ReadStrengths(merged));
    }

    [Fact]
    public void Reading_a_malformed_or_absent_document_is_empty_not_an_exception()
    {
        foreach (var input in new[] { null, "", "   ", "{", "[]", "\"a string\"", "123" })
        {
            Assert.Empty(RecommendationFactors.ReadStrengths(input));
            Assert.Empty(RecommendationFactors.ReadGaps(input));
            Assert.Empty(RecommendationFactors.ReadScores(input));
        }
    }

    /// <summary>
    /// A list entry that is not a string -- an object, a number, null -- is
    /// skipped rather than throwing on the cast.
    /// </summary>
    [Fact]
    public void ReadStringList_skips_entries_of_the_wrong_type()
    {
        var json = """{"strengths":["React", 42, null, {"a":1}, "  ", "C#"]}""";

        Assert.Equal(new[] { "React", "C#" }, RecommendationFactors.ReadStrengths(json));
    }

    /// <summary>
    /// Factors are labelled for a person, in the order they are worth reading.
    /// </summary>
    [Fact]
    public void ReadScores_labels_the_five_known_factors_in_order()
    {
        const string json = """
            {"budget_fit":0.85,"skill_coverage":0.9,"past_success_rate":1.0,
             "availability_and_speed":0.6,"portfolio_relevance":0.75}
            """;

        var scores = RecommendationFactors.ReadScores(json);

        Assert.Equal(
            new[] { "Skill coverage", "Relevant past work", "Past success rate", "Availability", "Budget fit" },
            scores.Select(s => s.Label));
        Assert.Equal(0.9m, scores[0].Value);
        Assert.Equal(0.85m, scores[4].Value);
    }

    /// <summary>
    /// A factor Python has added and C# has not heard of is still readable by
    /// name, so the two halves do not have to ship in the same commit.
    /// </summary>
    [Fact]
    public void ReadScores_ignores_unknown_keys_without_failing()
    {
        var json = """{"skill_coverage":0.9,"time_zone_overlap":0.4}""";

        var scores = RecommendationFactors.ReadScores(json);

        Assert.Single(scores);
        Assert.Equal("Skill coverage", scores[0].Label);
    }

    /// <summary>
    /// A model returning a very long string must not become a layout problem on
    /// a phone.
    /// </summary>
    [Fact]
    public void Merge_truncates_an_absurdly_long_entry()
    {
        var merged = RecommendationFactors.Merge(
            null, new List<string> { new string('x', 5000) }, null);

        var strength = Assert.Single(RecommendationFactors.ReadStrengths(merged));
        Assert.True(strength.Length <= 200, $"entry was {strength.Length} characters");
    }

    /// <summary>
    /// Truncation prefers a word boundary, so a cut does not land mid-word.
    /// </summary>
    [Fact]
    public void Merge_truncates_on_a_word_boundary_where_there_is_one()
    {
        var words = string.Join(" ", Enumerable.Repeat("excellent", 100));
        var merged = RecommendationFactors.Merge(null, new List<string> { words }, null);

        var strength = Assert.Single(RecommendationFactors.ReadStrengths(merged));
        Assert.DoesNotContain("excellen ", strength);
        Assert.Equal("excellent", strength.Split(' ').Last());
    }
}
