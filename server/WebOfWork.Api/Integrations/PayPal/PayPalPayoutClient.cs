using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WebOfWork.Api.Configuration;

namespace WebOfWork.Api.Integrations.PayPal;

public interface IPayPalPayoutClient
{
    Task<bool> SendPayoutAsync(string recipientEmail, decimal amount, string note);
}

// Internal shapes
file sealed class PayPalTokenResponse
{
    [JsonPropertyName("access_token")] public string AccessToken { get; set; } = string.Empty;
}

public class PayPalPayoutClient : IPayPalPayoutClient
{
    private readonly HttpClient _http;
    private readonly PayPalOptions _opts;
    private readonly ILogger<PayPalPayoutClient> _logger;
    private readonly string _baseUrl;

    public PayPalPayoutClient(HttpClient http, IOptions<PayPalOptions> opts, ILogger<PayPalPayoutClient> logger)
    {
        _http = http;
        _opts = opts.Value;
        _logger = logger;
        _baseUrl = _opts.Mode == "live" ? "https://api-m.paypal.com" : "https://api-m.sandbox.paypal.com";
    }

    private async Task<string> GetAccessTokenAsync()
    {
        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_opts.ClientId}:{_opts.Secret}"));
        var request = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/v1/oauth2/token");
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
        request.Content = new StringContent("grant_type=client_credentials", Encoding.UTF8, "application/x-www-form-urlencoded");
        var response = await _http.SendAsync(request);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync();
        var token = JsonSerializer.Deserialize<PayPalTokenResponse>(json);
        return token?.AccessToken ?? throw new InvalidOperationException("PayPal token response was empty.");
    }

    public async Task<bool> SendPayoutAsync(string recipientEmail, decimal amount, string note)
    {
        try
        {
            var token = await GetAccessTokenAsync();
            var payload = new
            {
                sender_batch_header = new { sender_batch_id = $"Payout_{Guid.NewGuid()}", email_subject = "You have a payout!" },
                items = new[]
                {
                    new
                    {
                        recipient_type = "EMAIL",
                        amount = new { value = Math.Round(amount, 2).ToString("0.00"), currency = _opts.Currency },
                        note = note,
                        receiver = recipientEmail
                    }
                }
            };

            var request = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/v1/payments/payouts");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            var response = await _http.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogError("PayPal payout failed: {StatusCode} {Error}", response.StatusCode, error);
                return false;
            }
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending PayPal payout to {Email}", recipientEmail);
            return false;
        }
    }
}
