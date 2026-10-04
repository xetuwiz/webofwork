using System.Text;

namespace WebOfWork.Api.Helpers;

/// <summary>
/// Enum names as the wire spells them.
/// </summary>
/// <remarks>
/// <para>
/// `Program.cs` registers
/// <c>JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower)</c>, so every
/// enum leaves the API as snake_case: <c>ContractStatus.UnderReview</c> is
/// <c>"under_review"</c>, never <c>1</c> and never <c>"UnderReview"</c>.
/// </para>
/// <para>
/// That is why hand-written error messages that echo the legal values back to a
/// client need this. Telling a caller "allowed: [Offered, Active, Completed]"
/// is actively unhelpful when the only spelling that will bind is
/// <c>"offered"</c>, <c>"active"</c>, <c>"completed"</c>.
/// </para>
/// <para>
/// This is the single implementation; <c>PaymentService</c> previously carried a
/// private copy, which is now delegating here.
/// </para>
/// </remarks>
public static class EnumNaming
{
    /// <summary>
    /// <c>UnderReview</c> -> <c>under_review</c>, matching
    /// <see cref="System.Text.Json.JsonNamingPolicy.SnakeCaseLower"/> for the
    /// simple PascalCase names used by every enum in this API.
    /// </summary>
    public static string ToSnakeCaseLower(this Enum value)
    {
        if (value is null) return string.Empty;

        var name = value.ToString();
        var sb = new StringBuilder(name.Length + 4);
        for (var i = 0; i < name.Length; i++)
        {
            if (char.IsUpper(name[i]) && i > 0) sb.Append('_');
            sb.Append(char.ToLowerInvariant(name[i]));
        }
        return sb.ToString();
    }

    /// <summary>
    /// Every member of <typeparamref name="TEnum"/> in its wire spelling, for
    /// use in a validation error's <c>allowed</c> list.
    /// </summary>
    public static string[] AllowedWireValues<TEnum>() where TEnum : struct, Enum
    {
        var all = (TEnum[])Enum.GetValues(typeof(TEnum));
        var result = new string[all.Length];
        for (var i = 0; i < all.Length; i++) result[i] = all[i].ToSnakeCaseLower();
        return result;
    }
}
