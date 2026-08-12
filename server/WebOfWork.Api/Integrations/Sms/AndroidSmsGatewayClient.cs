namespace WebOfWork.Api.Integrations.Sms;

/// <summary>
/// Typed HttpClient for the Android SMS Gateway (api.sms-gate.app).
/// Replaces Twilio/Vonage. Full implementation wired in Phase 1 when
/// strongly-typed AndroidSmsGatewayOptions are registered.
/// </summary>
public sealed class AndroidSmsGatewayClient
{
    private readonly HttpClient _http;

    public AndroidSmsGatewayClient(HttpClient http)
    {
        _http = http;
    }

    // Full Send() implementation added in Phase 1 (chore: configure android sms gateway sender).
}
