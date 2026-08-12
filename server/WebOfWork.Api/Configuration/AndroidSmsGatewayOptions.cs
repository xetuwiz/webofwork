namespace WebOfWork.Api.Configuration;

/// <summary>
/// Typed options for the Android SMS Gateway (api.sms-gate.app).
/// Replaces Twilio/Vonage — NEVER add Twilio references.
/// Username/Password confirmed in demo2/application.properties (sms.gateway.*).
/// </summary>
public sealed class AndroidSmsGatewayOptions
{
    public const string Section = "AndroidSmsGateway";

    public string Url                 { get; init; } = "https://api.sms-gate.app/3rdparty/v1";
    public string Username            { get; init; } = string.Empty;
    public string Password            { get; init; } = string.Empty;
    public int    SimNumber           { get; init; } = 1;
    public string DefaultCountryCode  { get; init; } = "+94";
    public bool   Enabled             { get; init; } = true;
}
