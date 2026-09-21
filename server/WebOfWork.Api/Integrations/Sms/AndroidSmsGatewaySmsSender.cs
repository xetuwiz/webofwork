using System.Net.Http;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WebOfWork.Api.Configuration;

namespace WebOfWork.Api.Integrations.Sms;

public class AndroidSmsGatewaySmsSender : ISmsSender
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<AndroidSmsGatewaySmsSender> _logger;
    private readonly AndroidSmsGatewayOptions _options;

    public AndroidSmsGatewaySmsSender(
        HttpClient httpClient,
        ILogger<AndroidSmsGatewaySmsSender> logger,
        IOptions<AndroidSmsGatewayOptions> options)
    {
        _httpClient = httpClient;
        _logger = logger;
        _options = options.Value;
    }

    public async Task SendSmsAsync(string phoneNumber, string message)
    {
        var normalizedPhone = NormalizeSriLankanPhoneNumber(phoneNumber);

        try
        {
            var payload = new
            {
                textMessage = new { text = message },
                simNumber = _options.SimNumber,
                phoneNumbers = new[] { normalizedPhone }
            };
            var response = await _httpClient.PostAsJsonAsync("messages", payload);
            response.EnsureSuccessStatusCode();
            _logger.LogInformation("SMS sent successfully to {Phone}", normalizedPhone);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to send SMS via Android gateway to {Phone}", normalizedPhone);
            throw;
        }
    }

    public static string NormalizeSriLankanPhoneNumber(string phoneNumber)
    {
        var digits = Regex.Replace(phoneNumber ?? string.Empty, @"\D", string.Empty);

        if (digits.StartsWith("0094", StringComparison.Ordinal))
        {
            digits = digits[2..];
        }

        if (digits.StartsWith("94", StringComparison.Ordinal) && digits.Length == 11 && digits[2] == '7')
        {
            return $"+{digits}";
        }

        if (digits.StartsWith("07", StringComparison.Ordinal) && digits.Length == 10)
        {
            return $"+94{digits[1..]}";
        }

        if (digits.Length == 9 && digits.StartsWith("7", StringComparison.Ordinal))
        {
            return $"+94{digits}";
        }

        throw new ArgumentException("Phone number must be a valid Sri Lankan mobile number.", nameof(phoneNumber));
    }
}
