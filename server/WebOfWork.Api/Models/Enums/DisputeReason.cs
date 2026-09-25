using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WebOfWork.Api.Models.Enums;

/// <summary>
/// The reference DisputeReason choices are inconsistent on purpose-mimicking: the
/// model tuple is ('scope_creep', ...), ('no_feedback', ...), but one entry is
/// literally ('Unlimited Revisions', ...) — capitals and a space. The client
/// mirrors that: DisputeModal.jsx posts "scope_creep", "no_feedback", "other"
/// and "Unlimited Revisions". The global JsonStringEnumConverter(SnakeCaseLower)
/// accepts the snake_case half and rejects the other, which 400s a dispute
/// reason that works in the reference.
///
/// This converter accepts every alias the reference can send and emits the reference
/// label on the way out, so <c>GET /disputes</c> shows the same string the
/// dropdown was populated from.
/// </summary>
[JsonConverter(typeof(DisputeReasonJsonConverter))]
public enum DisputeReason
{
    ScopeCreep = 1,
    NoFeedback = 2,
    UnlimitedRevisions = 3,
    PoorQuality = 4,
    MissedDeadline = 5,
    IncompleteWork = 6,
    CommunicationIssue = 7,
    Other = 8
}

public sealed class DisputeReasonJsonConverter : JsonConverter<DisputeReason>
{
    // Canonical the reference wire label per member. Read by DisputeReasonCatalog.
    internal static readonly Dictionary<DisputeReason, string> Labels = new()
    {
        [DisputeReason.ScopeCreep] = "scope_creep",
        [DisputeReason.NoFeedback] = "no_feedback",
        // Not snake_case: this is the literal value in the reference choice tuple.
        [DisputeReason.UnlimitedRevisions] = "Unlimited Revisions",
        [DisputeReason.PoorQuality] = "poor_quality",
        [DisputeReason.MissedDeadline] = "missed_deadline",
        [DisputeReason.IncompleteWork] = "incomplete_work",
        [DisputeReason.CommunicationIssue] = "communication_issue",
        [DisputeReason.Other] = "other",
    };

    // Everything the client or a direct API caller might send, normalised to a member.
    private static readonly Dictionary<string, DisputeReason> Aliases = BuildAliases();

    private static Dictionary<string, DisputeReason> BuildAliases()
    {
        var map = new Dictionary<string, DisputeReason>(StringComparer.OrdinalIgnoreCase);
        foreach (var (value, label) in Labels) map[label] = value;
        foreach (DisputeReason value in Enum.GetValues<DisputeReason>())
        {
            map[value.ToString()] = value;                       // UnlimitedRevisions
            map[value.ToString().ToLowerInvariant()] = value;    // scopecreep
            map[Snake(value)] = value;                            // scope_creep
        }
        return map;
    }

    internal static string Snake(DisputeReason value)
    {
        var name = value.ToString();
        var sb = new System.Text.StringBuilder(name.Length + 4);
        for (var i = 0; i < name.Length; i++)
        {
            if (i > 0 && char.IsUpper(name[i]) && !char.IsUpper(name[i - 1])) sb.Append('_');
            sb.Append(char.ToLowerInvariant(name[i]));
        }
        return sb.ToString();
    }

    public override DisputeReason Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        // Accept a bare string or a number, so int-ish clients also bind.
        if (reader.TokenType == JsonTokenType.Number && reader.TryGetInt32(out var numeric))
        {
            if (Enum.IsDefined(typeof(DisputeReason), numeric)) return (DisputeReason)numeric;
            throw new JsonException($"Unknown DisputeReason value {numeric}.");
        }

        var raw = reader.GetString();
        if (raw != null && Aliases.TryGetValue(raw.Trim(), out var parsed)) return parsed;

        throw new JsonException(
            $"Unknown DisputeReason '{raw}'. Expected one of: {string.Join(", ", Labels.Values)}.");
    }

    public override void Write(Utf8JsonWriter writer, DisputeReason value, JsonSerializerOptions options)
    {
        if (Labels.TryGetValue(value, out var label)) writer.WriteStringValue(label);
        else writer.WriteStringValue(Snake(value));
    }
}

/// <summary>One reason as a client needs it: the exact string to POST, plus a label to show.</summary>
public sealed record DisputeReasonOption(string Value, string Label);

/// <summary>
/// Read-only view of the dispute-reason vocabulary, exposed by
/// <c>GET /api/disputes/reasons</c> so a client never duplicates the list or its labels.
/// </summary>
public static class DisputeReasonCatalog
{
    /// <summary>Human-readable text. Separate from the wire value, which for
    /// <see cref="DisputeReason.UnlimitedRevisions"/> is the literal "Unlimited Revisions"
    /// and is not something a UI should print as-is.</summary>
    private static readonly Dictionary<DisputeReason, string> DisplayLabels = new()
    {
        [DisputeReason.ScopeCreep] = "Scope creep",
        [DisputeReason.NoFeedback] = "No feedback",
        [DisputeReason.UnlimitedRevisions] = "Unlimited revisions",
        [DisputeReason.PoorQuality] = "Poor quality",
        [DisputeReason.MissedDeadline] = "Missed deadline",
        [DisputeReason.IncompleteWork] = "Incomplete work",
        [DisputeReason.CommunicationIssue] = "Communication issue",
        [DisputeReason.Other] = "Other",
    };

    /// <summary>Ordered by enum value, i.e. the order the dropdown presents.</summary>
    public static IReadOnlyList<DisputeReasonOption> All { get; } =
        Enum.GetValues<DisputeReason>()
            .Select(v => new DisputeReasonOption(
                DisputeReasonJsonConverter.Labels.TryGetValue(v, out var label)
                    ? label
                    : DisputeReasonJsonConverter.Snake(v),
                DisplayLabels.TryGetValue(v, out var display)
                    ? display
                    : DisputeReasonJsonConverter.Snake(v)))
            .ToList();
}
