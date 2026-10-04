namespace WebOfWork.Api.DTOs.Auth;

/// <summary>
/// Body for POST /api/auth/refresh.
///
/// Browsers cannot read the HttpOnly refresh_token cookie from JavaScript, but
/// neither can a native mobile client -- there is no cookie jar. The body is
/// therefore an alternative to the cookie, not a replacement: Refresh() still
/// prefers the cookie when present so the existing web clients are unaffected.
/// </summary>
public sealed record RefreshTokenRequest(string? Refresh = null);
