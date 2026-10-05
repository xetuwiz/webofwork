using System.Text.Json;
using System.Text.Json.Nodes;

namespace WebOfWork.Api.Helpers;

/// <summary>
/// One reader and writer for the <c>AgentRecommendation.FactorsJson</c> document.
/// </summary>
/// <remarks>
/// <para>
/// That column holds a free-form JSON object written by the Python matching
/// agent: five named numeric factors, and -- since this helper existed -- the
/// <c>strengths</c> and <c>gaps</c> lists that explain them.
/// </para>
/// <para>
/// It is free-form because the agent owns the vocabulary of a factor and the
/// scoring model will gain factors. The cost of that choice is that the shape is
/// agreed in two languages and nowhere enforced, so the agreement lives here
/// instead: Python writes the keys, this class is the only C# that has to know
/// them, and a key that is missing reads as absent rather than throwing.
/// </para>
/// <para>
/// Never throws. A malformed blob costs a reader its factor breakdown, which is
/// a cosmetic loss; a malformed blob that throws costs a freelancer the whole
/// invitation.
/// </para>
/// </remarks>
public static class RecommendationFactors
{
    /// <summary>Key holding the favourable factors.</summary>
    public const string StrengthsKey = "strengths";

    /// <summary>Key holding the missing skills.</summary>
    public const string GapsKey = "gaps";

    /// <summary>
    /// The factor names, in the order they are worth explaining. Any key not
    /// listed is still read and returned, so adding a factor in Python does not
    /// require a deploy here to have it appear.
    /// </summary>
    private static readonly (string Key, string Label)[] DescribedFactors =
    {
        ("skill_coverage", "Skill coverage"),
        ("portfolio_relevance", "Relevant past work"),
        ("past_success_rate", "Past success rate"),
        ("availability_and_speed", "Availability"),
        ("budget_fit", "Budget fit"),
    };

    /// <summary>
    /// Fold <paramref name="strengths"/> and <paramref name="gaps"/> into the
    /// existing factors document and return the serialised result.
    /// </summary>
    /// <remarks>
    /// A non-numeric or unparseable <paramref name="factorsJson"/> yields a
    /// document holding only the lists, rather than the original string passed
    /// through. Keeping a string that is not a JSON object would guarantee the
    /// two lists are lost, since there is nowhere in it to put them.
    /// </remarks>
    public static string? Merge(string? factorsJson, List<string>? strengths, List<string>? gaps)
    {
        // Nothing to add and nothing to preserve: stay null rather than writing
        // "{}" , which would read as "scored, with no factors" rather than
        // "not scored".
        if ((strengths is null || strengths.Count == 0) && (gaps is null || gaps.Count == 0))
        {
            // Whitespace normalises to null too. An empty string in a nullable
            // column is the worst of the three options: it is not null so a
            // "was this scored?" check says yes, and it is not parseable so the
            // reader then has to handle it as a failure.
            return string.IsNullOrWhiteSpace(factorsJson) ? null : factorsJson;
        }

        var node = Parse(factorsJson) as JsonObject ?? new JsonObject();

        if (strengths is { Count: > 0 }) node[StrengthsKey] = ToArray(strengths);
        if (gaps is { Count: > 0 }) node[GapsKey] = ToArray(gaps);

        return node.ToJsonString();
    }

    /// <summary>
    /// The named numeric factors, as a human-readable label to value.
    /// </summary>
    public static IReadOnlyList<(string Label, decimal Value)> ReadScores(string? factorsJson)
    {
        var results = new List<(string, decimal)>();
        var node = Parse(factorsJson);
        if (node is not JsonObject obj) return results;

        foreach (var (key, label) in DescribedFactors)
        {
            if (obj.TryGetPropertyValue(key, out var value) && value is JsonValue v
                && v.TryGetValue<decimal>(out var number))
            {
                results.Add((label, number));
            }
        }

        return results;
    }

    /// <summary>The <c>strengths</c> list, or an empty list.</summary>
    public static List<string> ReadStrengths(string? factorsJson) => ReadStringList(factorsJson, StrengthsKey);

    /// <summary>The <c>gaps</c> list, or an empty list.</summary>
    public static List<string> ReadGaps(string? factorsJson) => ReadStringList(factorsJson, GapsKey);

    private static List<string> ReadStringList(string? factorsJson, string key)
    {
        var results = new List<string>();
        if (Parse(factorsJson) is not JsonObject obj) return results;
        if (!obj.TryGetPropertyValue(key, out var value) || value is not JsonArray array) return results;

        foreach (var item in array)
        {
            if (item is JsonValue jv && jv.TryGetValue<string>(out var s))
            {
                var trimmed = s.Trim();
                // A blank entry is noise from the model, not information.
                if (trimmed.Length > 0) results.Add(trimmed);
            }
        }

        return results;
    }

    private static JsonNode? Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            return JsonNode.Parse(json);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private const int MaxEntryLength = 200;

    private static string Truncate(string value)
    {
        if (value.Length <= MaxEntryLength) return value;

        var cut = value.LastIndexOf(' ', MaxEntryLength);
        // A single 200-character "word" -- usually a URL -- has no space to cut
        // on, so fall back to the hard limit rather than dropping the entry.
        return value[..(cut > MaxEntryLength / 2 ? cut : MaxEntryLength)];
    }

    private static JsonArray ToArray(List<string> values)
    {
        var array = new JsonArray();
        foreach (var value in values)
        {
            // A cap, because this text is shown to a freelancer on a phone and a
            // runaway list is a layout bug rather than information. Truncated on a
            // word boundary where there is one, so a cut does not land mid-word.
            array.Add(Truncate(value));
        }

        return array;
    }
}
