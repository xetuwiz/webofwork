namespace WebOfWork.Api.Configuration;

/// <summary>
/// Platform-wide defaults.
/// DefaultCurrency is always "USD" — this is the critical money invariant.
/// Never change away from USD. All decimal(18,2) monetary values use this currency.
/// </summary>
public sealed class PlatformOptions
{
    public const string Section = "Platform";

    /// <summary>Must always be "USD". Never LKR or EUR.</summary>
    public string DefaultCurrency { get; init; } = "USD";
}
