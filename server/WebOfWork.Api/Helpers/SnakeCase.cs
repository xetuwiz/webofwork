namespace WebOfWork.Api.Helpers;

using System.Text;

/// <summary>
/// Renders enum values the way DRF renders <c>choices=[...]</c> — lowercase snake_case
/// ("in_progress", "pending", "refunded"). the reference serialises the enum's lowercase value, so
/// anything the frontend compares against a literal (e.g. <c>status === "in_progress"</c>) or
/// renders directly must use this casing.
/// <para>
/// Preferred path: declare the DTO property as the enum type so the global
/// <c>JsonStringEnumConverter(SnakeCaseLower)</c> in Program.cs handles it automatically.
/// Use this helper only for anonymous projections and other string-typed members.
/// </para>
/// </summary>
public static class SnakeCase
{
    public static string Of<TEnum>(TEnum value) where TEnum : struct, Enum
    {
        var name = value.ToString();
        if (name.Length <= 1) return name.ToLowerInvariant();

        var sb = new StringBuilder(name.Length + 4);
        for (var i = 0; i < name.Length; i++)
        {
            var c = name[i];
            if (!char.IsUpper(c)) { sb.Append(c); continue; }

            if (i > 0)
            {
                // name[i - 1] must stay inside this guard: at i == 0 there is no previous
                // character, and reading it there threw IndexOutOfRangeException.
                var prev = name[i - 1];
                var nextIsLower = i + 1 < name.Length && char.IsLower(name[i + 1]);
                if (!char.IsUpper(prev) || nextIsLower) sb.Append('_');
            }
            sb.Append(char.ToLowerInvariant(c));
        }
        return sb.ToString();
    }
}