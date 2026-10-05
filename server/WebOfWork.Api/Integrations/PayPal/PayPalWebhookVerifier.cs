using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WebOfWork.Api.Configuration;

namespace WebOfWork.Api.Integrations.PayPal;

public interface IPayPalWebhookVerifier
{
    Task<bool> VerifyAsync(IHeaderDictionary headers, string rawBody, CancellationToken ct);
}

file sealed class PayPalWebhookTokenResponse
{
    [JsonPropertyName("access_token")] public string AccessToken { get; set; } = string.Empty;
}

file sealed class PayPalWebhookVerificationResponse
{
    [JsonPropertyName("verification_status")] public string VerificationStatus { get; set; } = string.Empty;
}

public sealed class PayPalWebhookVerifier : IPayPalWebhookVerifier
{
    private readonly HttpClient _http;
    private readonly PayPalOptions _opts;
    private readonly ILogger<PayPalWebhookVerifier> _logger;
    private readonly string _baseUrl;

    public PayPalWebhookVerifier(HttpClient http, IOptions<PayPalOptions> opts, ILogger<PayPalWebhookVerifier> logger)
    {
        _http = http;
        _opts = opts.Value;
        _logger = logger;
        _baseUrl = _opts.Mode == "live" ? "https://api-m.paypal.com" : "https://api-m.sandbox.paypal.com";
    }

    public async Task<bool> VerifyAsync(IHeaderDictionary headers, string rawBody, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_opts.WebhookId))
        {
            _logger.LogWarning("PayPal webhook verification failed because PayPal:WebhookId is not configured.");
            return false;
        }

        try
        {
            var token = await GetAccessTokenAsync(ct);
            using var webhookEvent = JsonDocument.Parse(rawBody);
            var payload = new Dictionary<string, object?>
            {
                ["auth_algo"] = Header(headers, "PAYPAL-AUTH-ALGO"),
                ["cert_url"] = Header(headers, "PAYPAL-CERT-URL"),
                ["transmission_id"] = Header(headers, "PAYPAL-TRANSMISSION-ID"),
                ["transmission_sig"] = Header(headers, "PAYPAL-TRANSMISSION-SIG"),
                ["transmission_time"] = Header(headers, "PAYPAL-TRANSMISSION-TIME"),
                ["webhook_id"] = _opts.WebhookId,
                ["webhook_event"] = webhookEvent.RootElement.Clone()
            };

            var request = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/v1/notifications/verify-webhook-signature");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            var response = await _http.SendAsync(request, ct);
            var json = await response.Content.ReadAsStringAsync(ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("PayPal webhook signature verification request failed (HTTP {StatusCode}): {Body}", (int)response.StatusCode, json);
                return false;
            }

            var result = JsonSerializer.Deserialize<PayPalWebhookVerificationResponse>(json);
            var status = result?.VerificationStatus ?? string.Empty;
            if (string.Equals(status, "SUCCESS", StringComparison.OrdinalIgnoreCase)
                || string.Equals(status, "VERIFIED", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation("PayPal webhook signature VERIFIED (Status={VerificationStatus}). TransmissionId={TransmissionId}",
                    status,
                    Header(headers, "PAYPAL-TRANSMISSION-ID"));
                return true;
            }

            _logger.LogWarning("PayPal webhook signature NOT verified. Status={VerificationStatus}. TransmissionId={TransmissionId} AuthAlgo={AuthAlgo} CertUrl={CertUrl} TransmissionTime={TransmissionTime} WebhookId={WebhookId}. Response body: {Body}",
                status,
                Header(headers, "PAYPAL-TRANSMISSION-ID"),
                Header(headers, "PAYPAL-AUTH-ALGO"),
                Header(headers, "PAYPAL-CERT-URL"),
                Header(headers, "PAYPAL-TRANSMISSION-TIME"),
                _opts.WebhookId,
                json);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "PayPal webhook signature verification failed.");
            return false;
        }
    }

    private async Task<string> GetAccessTokenAsync(CancellationToken ct)
    {
        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_opts.ClientId}:{_opts.Secret}"));
        var request = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/v1/oauth2/token");
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
        request.Content = new StringContent("grant_type=client_credentials", Encoding.UTF8, "application/x-www-form-urlencoded");

        var response = await _http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync(ct);
        var token = JsonSerializer.Deserialize<PayPalWebhookTokenResponse>(json);
        return token?.AccessToken ?? throw new InvalidOperationException("PayPal token response was empty.");
    }

    private static string Header(IHeaderDictionary headers, string name)
        => headers.TryGetValue(name, out var value) ? value.ToString() : string.Empty;
}
