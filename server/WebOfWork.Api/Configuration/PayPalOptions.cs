namespace WebOfWork.Api.Configuration;

/// <summary>
/// PayPal Checkout SDK options.
/// Currency is always LKR — never USD/EUR (engineering standard).
/// Mode: "sandbox" in dev, "live" in prod via Railway env vars.
/// </summary>
public sealed class PayPalOptions
{
    public const string Section = "PayPal";

    public string ClientId  { get; init; } = string.Empty;
    public string Secret    { get; init; } = string.Empty;
    /// <summary>"sandbox" or "live". Injected via env in Railway for prod.</summary>
    public string Mode      { get; init; } = "sandbox";
    /// <summary>Always "LKR" — never USD or EUR.</summary>
    public string Currency  { get; init; } = "LKR";
    public string WebhookId { get; init; } = string.Empty;
}
