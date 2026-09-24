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

public interface IPayPalClient
{
    Task<PayPalOrderDetails?> GetOrderDetailsAsync(string orderId);
}

public class PayPalOrderDetails
{
    public string OrderId   { get; set; } = string.Empty;
    public string Status    { get; set; } = string.Empty;
    public decimal Amount   { get; set; }
    public string Currency  { get; set; } = string.Empty;
    public string? CaptureId { get; set; }
}

// ── Internal DTO shapes from PayPal API ────────────────────────────────────
file sealed class PayPalTokenResponse
{
    [JsonPropertyName("access_token")] public string AccessToken { get; set; } = string.Empty;
    [JsonPropertyName("token_type")]   public string TokenType   { get; set; } = string.Empty;
}

file sealed class PayPalOrderResponse
{
    [JsonPropertyName("id")]              public string Id     { get; set; } = string.Empty;
    [JsonPropertyName("status")]          public string Status { get; set; } = string.Empty;
    [JsonPropertyName("purchase_units")]  public PayPalPurchaseUnit[]? PurchaseUnits { get; set; }
}

file sealed class PayPalPurchaseUnit
{
    [JsonPropertyName("amount")] public PayPalAmount? Amount { get; set; }
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

file sealed class PayPalAmount
{
    [JsonPropertyName("currency_code")] public string CurrencyCode { get; set; } = string.Empty;
    [JsonPropertyName("value")]         public string Value        { get; set; } = string.Empty;
}

// ── Real PayPal REST Client ─────────────────────────────────────────────────
public class PayPalClient : IPayPalClient
{
    private readonly HttpClient         _http;
    private readonly PayPalOptions      _opts;
    private readonly ILogger<PayPalClient> _logger;
    private readonly string             _baseUrl;

    public PayPalClient(HttpClient http, IOptions<PayPalOptions> opts, ILogger<PayPalClient> logger)
    {
        _http    = http;
        _opts    = opts.Value;
        _logger  = logger;
        _baseUrl = _opts.Mode == "live"
            ? "https://api-m.paypal.com"
            : "https://api-m.sandbox.paypal.com";
    }

    private async Task<string> GetAccessTokenAsync()
    {
        var credentials = Convert.ToBase64String(
            Encoding.UTF8.GetBytes($"{_opts.ClientId}:{_opts.Secret}"));

        var request = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/v1/oauth2/token");
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
        request.Content = new StringContent("grant_type=client_credentials",
            Encoding.UTF8, "application/x-www-form-urlencoded");

        var response = await _http.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        var token = JsonSerializer.Deserialize<PayPalTokenResponse>(json);
        return token?.AccessToken ?? throw new InvalidOperationException("PayPal token response was empty.");
    }

    public async Task<PayPalOrderDetails?> GetOrderDetailsAsync(string orderId)
    {
        try
        {
            var token = await GetAccessTokenAsync();

            var request = new HttpRequestMessage(HttpMethod.Get,
                $"{_baseUrl}/v2/checkout/orders/{orderId}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await _http.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("PayPal order lookup failed: {Status}", response.StatusCode);
                return null;
            }

            var json = await response.Content.ReadAsStringAsync();
            var order = JsonSerializer.Deserialize<PayPalOrderResponse>(json);
            if (order == null) return null;

            var unit   = order.PurchaseUnits?[0];
            var amount = decimal.TryParse(unit?.Amount?.Value, out var v) ? v : 0m;

            return new PayPalOrderDetails
            {
                OrderId  = order.Id,
                Status   = order.Status,
                Amount   = amount,
                Currency = unit?.Amount?.CurrencyCode ?? string.Empty,
                CaptureId = unit?.Payments?.Captures?.FirstOrDefault()?.Id
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching PayPal order {OrderId}", orderId);
            return null;
        }
    }
}
