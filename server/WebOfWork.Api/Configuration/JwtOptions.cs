namespace WebOfWork.Api.Configuration;

public sealed class JwtOptions
{
    public const string Section = "Jwt";

    public string Issuer                  { get; init; } = "WebOfWork";
    public string Audience                { get; init; } = "WebOfWork";
    /// <summary>HMAC-SHA256 signing secret. Min 32 bytes. Set via env/secrets — never hardcode.</summary>
    public string Secret                  { get; init; } = string.Empty;
    /// <summary>Separate secret for refresh-token signing to limit blast radius.</summary>
    public string RefreshSecret           { get; init; } = string.Empty;
    /// <summary>Access token TTL in minutes. Grolance legacy used 1 min — too short. Default 60.</summary>
    public int    AccessTokenExpiryMinutes { get; init; } = 60;
    /// <summary>Sliding refresh window in days.</summary>
    public int    RefreshTokenExpiryDays  { get; init; } = 7;
}
