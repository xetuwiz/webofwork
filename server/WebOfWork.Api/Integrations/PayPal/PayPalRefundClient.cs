using System;
using System.Collections.Generic;
using System.Linq;
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

public interface IPayPalRefundClient
{
    Task<bool> RefundOrderAsync(string orderId, decimal amount, string reason);
}

file sealed class PayPalTokenResponse
{
    [JsonPropertyName("access_token")] public string AccessToken { get; set; } = string.Empty;
}
file sealed class PayPalOrderResponse
{
    [JsonPropertyName("purchase_units")] public PayPalPurchaseUnit[]? PurchaseUnits { get; set; }
}
file sealed class PayPalPurchaseUnit
{
    [JsonPropertyName("payments")] public PayPalPayments? Payments { get; set; }
}
file sealed class PayPalPayments
{
    [JsonPropertyName("captures")] public PayPalCapture[]? Captures { get; set; }
}
file sealed class PayPalCapture
{
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
}
file sealed class PayPalErrorDetail
{
    [JsonPropertyName("issue")] public string Issue { get; set; } = string.Empty;
    [JsonPropertyName("description")] public string Description { get; set; } = string.Empty;
}
file sealed class PayPalErrorResponse
{
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
    [JsonPropertyName("message")] public string Message { get; set; } = string.Empty;
    [JsonPropertyName("debug_id")] public string DebugId { get; set; } = string.Empty;
    [JsonPropertyName("details")] public PayPalErrorDetail[]? Details { get; set; }
}

public class PayPalRefundClient : IPayPalRefundClient
{
    private readonly HttpClient _http;
    private readonly PayPalOptions _opts;
    private readonly ILogger<PayPalRefundClient> _logger;
    private readonly string _baseUrl;

    public PayPalRefundClient(HttpClient http, IOptions<PayPalOptions> opts, ILogger<PayPalRefundClient> logger)
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

    public async Task<bool> RefundOrderAsync(string orderId, decimal amount, string reason)
    {
        var token = await GetAccessTokenAsync();

        // 1. Get Capture ID from Order ID
        var req1 = new HttpRequestMessage(HttpMethod.Get, $"{_baseUrl}/v2/checkout/orders/{orderId}");
        req1.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var res1 = await _http.SendAsync(req1);
        if (!res1.IsSuccessStatusCode)
        {
            var error = TryParsePayPalError(await res1.Content.ReadAsStringAsync());
            throw new InvalidOperationException(
                $"PayPal order lookup failed ({(int)res1.StatusCode}): {error.Message}"
                + (!string.IsNullOrEmpty(error.DetailsFlat) ? $" Details: {error.DetailsFlat}" : string.Empty)
                + (!string.IsNullOrEmpty(error.DebugId) ? $" (debug_id: {error.DebugId})" : string.Empty));
        }

        var orderData = JsonSerializer.Deserialize<PayPalOrderResponse>(await res1.Content.ReadAsStringAsync());
        var captureId = orderData?.PurchaseUnits?[0]?.Payments?.Captures?[0]?.Id;
        if (string.IsNullOrEmpty(captureId))
        {
            _logger.LogWarning("No capture ID found for order {OrderId}", orderId);
            throw new InvalidOperationException($"PayPal refund failed: no capture ID found for order {orderId}. The payment may not have been captured yet.");
        }

        // 2. Refund the Capture
        var payload = new
        {
            amount = new { value = Math.Round(amount, 2).ToString("0.00"), currency_code = _opts.Currency },
            note_to_payer = reason
        };
        var req2 = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/v2/payments/captures/{captureId}/refund");
        req2.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        req2.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        var res2 = await _http.SendAsync(req2);
        if (!res2.IsSuccessStatusCode)
        {
            var rawError = await res2.Content.ReadAsStringAsync();
            _logger.LogError("PayPal refund failed: {StatusCode} {Error}", res2.StatusCode, rawError);
            var error = TryParsePayPalError(rawError);
            throw new InvalidOperationException(
                $"PayPal refund failed ({(int)res2.StatusCode}): {error.Message}"
                + (!string.IsNullOrEmpty(error.DetailsFlat) ? $" | {error.DetailsFlat}" : string.Empty)
                + (!string.IsNullOrEmpty(error.DebugId) ? $" (debug_id: {error.DebugId})" : string.Empty));
        }
        return true;
    }

    private static (string Message, string DetailsFlat, string DebugId) TryParsePayPalError(string rawBody)
    {
        try
        {
            var err = JsonSerializer.Deserialize<PayPalErrorResponse>(rawBody);
            if (err == null) return (rawBody, string.Empty, string.Empty);
            var detailsFlat = err.Details != null && err.Details.Length > 0
                ? string.Join("; ", err.Details.Select(d => $"{d.Issue}: {d.Description}"))
                : string.Empty;
            return (err.Message, detailsFlat, err.DebugId);
        }
        catch
        {
            return (rawBody, string.Empty, string.Empty);
        }
    }
}
