namespace WebOfWork.Api.Configuration;

/// <summary>
/// Platform-wide defaults.
/// DefaultCurrency is always "LKR" — this is the critical money invariant.
/// Never change to USD or EUR. All decimal(18,2) monetary values use this currency.
/// </summary>
public sealed class PlatformOptions
{
    public const string Section = "Platform";

    /// <summary>Must always be "LKR". Never USD or EUR.</summary>
    public string DefaultCurrency { get; init; } = "LKR";
}
