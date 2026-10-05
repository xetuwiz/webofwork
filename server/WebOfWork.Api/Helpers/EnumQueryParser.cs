using System.Text.RegularExpressions;

namespace WebOfWork.Api.Helpers;

/// <summary>
/// Parses snake_case enum values coming from the frontend (the reference sends "in_progress", "open", ...).
/// <see cref="Enum.TryParse{TEnum}(string, bool, out TEnum)"/> does not bridge the gap because
/// "in_progress" != "InProgress", so an unparsable value silently becomes "no filter" and the
/// endpoint returns the unfiltered set instead of an error.
/// </summary>
public static class EnumQueryParser
{
    public static bool TryParse<TEnum>(string? value, out TEnum result) where TEnum : struct, Enum
    {
        result = default;
        if (string.IsNullOrWhiteSpace(value)) return false;

        if (Enum.TryParse<TEnum>(value.Trim(), true, out result)) return true;

        // Normalise both sides: "in_progress" -> "InProgress", "IN PROGRESS" -> "InProgress".
        var normalized = Regex.Replace(value.Trim(), "[^A-Za-z0-9]", string.Empty).ToLowerInvariant();
        foreach (var name in Enum.GetNames<TEnum>())
        {
            var candidate = name.ToLowerInvariant();
            if (candidate == normalized)
            {
                result = Enum.Parse<TEnum>(name);
                return true;
            }
        }
        return false;
    }
}
