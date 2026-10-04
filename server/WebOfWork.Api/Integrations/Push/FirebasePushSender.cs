using System.Text.Json;
using System.Text.Json.Serialization;
using Google.Apis.Auth.OAuth2;

namespace WebOfWork.Api.Integrations.Push;

/// <summary>
/// FCM HTTP v1 sender.
///
/// Why v1 and not the legacy endpoint: legacy FCM is shut down, and its auth was
/// a long-lived server key pasted into config. v1 uses the same OAuth2 service
/// account the rest of this project already has a package for
/// (<c>Google.Apis.Auth</c>), so there is no new dependency and no static secret
/// to rotate.
///
/// Configured from <c>Firebase:ServiceAccountPath</c> + <c>Firebase:ProjectId</c>.
/// Both being absent is the supported "push is off" state, not a startup
/// failure -- see <see cref="NoOpPushSender"/>.
/// </summary>
public sealed class FirebasePushSender : IPushSender, IDisposable
{
    private const string Scope = "https://www.googleapis.com/auth/firebase.messaging";

    private readonly HttpClient _http;
    private readonly ILogger<FirebasePushSender> _logger;
    private readonly string _projectId;
    private readonly SemaphoreSlim _tokenLock = new(1, 1);
    private GoogleCredential? _credential;
    private string? _accessToken;
    private DateTimeOffset _accessTokenExpiry = DateTimeOffset.MinValue;

    public bool IsConfigured { get; }

    public FirebasePushSender(
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory,
        ILogger<FirebasePushSender> logger)
    {
        _logger = logger;
        _projectId = configuration["Firebase:ProjectId"] ?? string.Empty;
        var serviceAccountPath = configuration["Firebase:ServiceAccountPath"] ?? string.Empty;

        // One shared client, allocated before any early return: the default
        // handler pools connections, and a new HttpClient per notification
        // exhausts sockets under load.
        _http = httpClientFactory.CreateClient(nameof(FirebasePushSender));
        _http.Timeout = TimeSpan.FromSeconds(15);

        if (string.IsNullOrWhiteSpace(_projectId) || string.IsNullOrWhiteSpace(serviceAccountPath))
        {
            _logger.LogWarning(
                "FCM is not configured (Firebase:ProjectId / Firebase:ServiceAccountPath missing). " +
                "Push notifications will not be delivered; in-app and SMS still work.");
            return;
        }

        if (!File.Exists(serviceAccountPath))
        {
            _logger.LogError(
                "FCM service account file not found at {Path}. Push notifications will not be delivered.",
                serviceAccountPath);
            return;
        }

        try
        {
            // `CredentialFactory` rather than the obsolete `GoogleCredential.FromJson`:
            // the latter is deprecated for exactly the risk this code would
            // otherwise hit -- it silently accepts a JSON containing unrelated
            // credential types. CredentialFactory fails closed on an unexpected
            // `type`. It also derives the signer key that makes the access token a
            // real OAuth2 JWT.
            _credential = CredentialFactory
                .FromJson<ServiceAccountCredential>(File.ReadAllText(serviceAccountPath))
                .ToGoogleCredential()
                .CreateScoped(Scope);
            IsConfigured = true;
        }
        catch (Exception ex)
        {
            // A malformed/expired credential must not take the whole API down --
            // the notification row and the in-app push are independent of it.
            _logger.LogError(ex, "Could not initialise the FCM credential. Push disabled.");
        }
    }

    public async Task<IReadOnlyList<PushResult>> SendAsync(
        IReadOnlyCollection<PushMessage> messages,
        CancellationToken ct = default)
    {
        if (!IsConfigured || messages.Count == 0)
            return Array.Empty<PushResult>();

        var results = new List<PushResult>(messages.Count);
        var token = await GetAccessTokenAsync(ct);
        if (token is null)
            return messages.Select(m => new PushResult(m.Token, false, Error: "no access token")).ToList();

        foreach (var message in messages)
        {
            ct.ThrowIfCancellationRequested();
            results.Add(await SendOneAsync(message, token, ct));
        }

        return results;
    }

    private async Task<PushResult> SendOneAsync(PushMessage message, string accessToken, CancellationToken ct)
    {
        var url = $"https://fcm.googleapis.com/v1/projects/{_projectId}/messages:send";
        var body = BuildBody(message);

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json"),
            };
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

            using var response = await _http.SendAsync(request, ct);
            if (response.IsSuccessStatusCode) return new PushResult(message.Token, true);

            var error = await response.Content.ReadAsStringAsync(ct);
            var invalid = IsTokenInvalid(response.StatusCode, error);

            // 401 here means the *access token* is stale, not the device token. The
            // next call must mint a fresh one, so drop the cached value.
            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                _accessToken = null;
                _accessTokenExpiry = DateTimeOffset.MinValue;
            }

            _logger.LogWarning("FCM rejected a push: {Status} {Error}", (int)response.StatusCode, error);
            return new PushResult(message.Token, false, invalid, error);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Network blips must not surface as a failed notification write.
            _logger.LogError(ex, "FCM request failed for a device token");
            return new PushResult(message.Token, false, Error: ex.Message);
        }
    }

    /// <summary>
    /// Whether FCM told us this token is permanently unusable, so the caller can
    /// delete the row.
    ///
    /// A stale token fails on *every* future notification, so failing to recognise
    /// one here means the table grows and every publish pays an extra doomed HTTP
    /// round trip per dead row. This is deliberately liberal: the cost of a false
    /// positive is one device re-registering on its next cold start, while the cost
    /// of a false negative is unbounded.
    ///
    /// The structured check matters more than the string matching. FCM v1 answers
    /// a malformed token with a `google.rpc.BadRequest` carrying
    /// `fieldViolations: [{ field: "message.token", description: "..." }]`, and the
    /// wording has already changed once -- an earlier version of this method only
    /// matched the legacy `registration-token-not-registered` string, so a real
    /// "not a valid FCM registration token" response was classified as *recoverable*
    /// and the row was kept forever.
    /// </summary>
    private static bool IsTokenInvalid(System.Net.HttpStatusCode status, string error)
    {
        if (string.IsNullOrEmpty(error)) return false;

        // UNREGISTERED on v1 arrives as 404: the app instance is gone.
        if (status == System.Net.HttpStatusCode.NotFound) return true;
        if (status != System.Net.HttpStatusCode.BadRequest) return false;

        if (MentionsTokenFieldViolation(error)) return true;

        return error.Contains("registration-token-not-registered", StringComparison.OrdinalIgnoreCase)
            || error.Contains("registration-token-mismatch", StringComparison.OrdinalIgnoreCase)
            || error.Contains("UNREGISTERED", StringComparison.OrdinalIgnoreCase)
            || error.Contains("InvalidRegistration", StringComparison.OrdinalIgnoreCase)
            || error.Contains("not a valid FCM registration token", StringComparison.OrdinalIgnoreCase)
            || error.Contains("Requested entity was not found", StringComparison.OrdinalIgnoreCase)
            || error.Contains("SENDER_ID_MISMATCH", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// True when the RFC 7807 body carries a `fieldViolations` entry naming
    /// `message.token`. Keyed off the field name rather than the human-readable
    /// description, so it survives FCM rewording the message.
    /// </summary>
    private static bool MentionsTokenFieldViolation(string error)
    {
        try
        {
            using var doc = JsonDocument.Parse(error);
            if (!doc.RootElement.TryGetProperty("error", out var errObj)) return false;
            if (!errObj.TryGetProperty("details", out var details)
                || details.ValueKind != JsonValueKind.Array)
                return false;

            foreach (var detail in details.EnumerateArray())
            {
                if (!detail.TryGetProperty("fieldViolations", out var violations)
                    || violations.ValueKind != JsonValueKind.Array)
                    continue;

                foreach (var violation in violations.EnumerateArray())
                {
                    if (violation.TryGetProperty("field", out var field)
                        && field.GetString() is { } name
                        && name.EndsWith("token", StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }
        }
        catch (JsonException)
        {
            // Not JSON -- the string fallbacks above are the only signal available.
        }

        return false;
    }

    /// <summary>
    /// Builds the v1 message envelope.
    ///
    /// Both `notification` and `data` are sent. `notification` alone is what the
    /// system tray renders while the app is backgrounded, but it is not delivered
    /// to `onMessage` at all in that state, so the app could not update its badge
    /// from it. `data` alone *is* delivered to `onMessage`, but then Android shows
    /// nothing in the shade on its own and the background handler has to build the
    /// notification. Sending both is the documented way to get tray display and
    /// client-side handling at once.
    /// </summary>
    private static string BuildBody(PushMessage message)
    {
        var data = new Dictionary<string, string>(StringComparer.Ordinal);
        if (message.Data is not null)
        {
            foreach (var (key, value) in message.Data)
                data[key] = value;
        }

        // `click_action` is read by the notification handler to route the tap.
        // Android needs the extra `_` prefix in the data payload for this to be
        // honoured from a data message.
        data["_click_action"] = data.TryGetValue("route", out var route) && !string.IsNullOrWhiteSpace(route)
            ? route
            : "default";

        return JsonSerializer.Serialize(
            new
            {
                message = new
                {
                    token = message.Token,
                    notification = new
                    {
                        title = message.Title,
                        body = message.Body,
                    },
                    data,
                    android = new
                    {
                        // HIGH so a contract dispute is not silently delayed behind
                        // a doze-windowed low-priority message.
                        priority = "HIGH",
                        // Lets the client collapse rapid duplicates (e.g. a client
                        // tapping "approve" twice) instead of stacking them.
                        collapse_key = "webofwork_notification",
                    },
                },
            },
            new JsonSerializerOptions { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull });
    }

    /// <summary>
    /// Mints (and caches) a service-account access token.
    ///
    /// `GetAccessTokenForRequestAsync` hands back a bare `string` with no expiry
    /// attached, so the lifetime is tracked here against Google's documented
    /// one-hour access-token TTL. The refresh happens under a lock so a burst of
    /// notifications does not mint a token each.
    /// </summary>
    private async Task<string?> GetAccessTokenAsync(CancellationToken ct)
    {
        if (_accessToken is not null && DateTimeOffset.UtcNow < _accessTokenExpiry) return _accessToken;

        await _tokenLock.WaitAsync(ct);
        try
        {
            if (_accessToken is not null && DateTimeOffset.UtcNow < _accessTokenExpiry) return _accessToken;

            if (_credential is null) return null;

            _accessToken = await _credential.UnderlyingCredential.GetAccessTokenForRequestAsync(Scope, ct);
            if (string.IsNullOrEmpty(_accessToken)) return null;

            // 50 minutes, not 60: a token that expires mid-flight is the classic
            // cause of intermittent 401s under load, and the refresh is cheap.
            _accessTokenExpiry = DateTimeOffset.UtcNow.AddMinutes(50);
            return _accessToken;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Could not obtain an FCM access token");
            return null;
        }
        finally
        {
            _tokenLock.Release();
        }
    }

    public void Dispose()
    {
        _tokenLock.Dispose();
    }
}
