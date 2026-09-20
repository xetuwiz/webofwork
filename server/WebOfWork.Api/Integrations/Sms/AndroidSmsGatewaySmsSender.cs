using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace WebOfWork.Api.Integrations.Sms;

public class AndroidSmsGatewaySmsSender : ISmsSender
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<AndroidSmsGatewaySmsSender> _logger;

    public AndroidSmsGatewaySmsSender(HttpClient httpClient, ILogger<AndroidSmsGatewaySmsSender> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task SendSmsAsync(string phoneNumber, string message)
    {
        // Formatting phone for the Android Gateway
        if (!phoneNumber.StartsWith("+"))
        {
            if (phoneNumber.StartsWith("0"))
                phoneNumber = "+94" + phoneNumber.Substring(1);
            else
                phoneNumber = "+94" + phoneNumber;
        }

        try
        {
            var payload = new
            {
                textMessage = new { text = message },
                simNumber = 1,
                phoneNumbers = new[] { phoneNumber }
            };
            var response = await _httpClient.PostAsJsonAsync("messages", payload);
            response.EnsureSuccessStatusCode();
            _logger.LogInformation("SMS sent successfully to {Phone}", phoneNumber);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to send SMS via Android gateway to {Phone}", phoneNumber);
            throw;
        }
    }
}
