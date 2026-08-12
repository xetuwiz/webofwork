namespace WebOfWork.Api.Configuration;

/// <summary>
/// Google OAuth options for social login token verification (Phase 3).
/// Confirmed in Grolance settings.py: GOOGLE_CLIENT_ID.
/// Google.Apis.Auth validates ID tokens server-side — no backend redirect needed.
/// </summary>
public sealed class GoogleOptions
{
    public const string Section = "Google";

    public string ClientId { get; init; } = string.Empty;
}
