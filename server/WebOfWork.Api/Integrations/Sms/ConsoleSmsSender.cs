using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace WebOfWork.Api.Integrations.Sms;

public class ConsoleSmsSender : ISmsSender
{
    private readonly ILogger<ConsoleSmsSender> _logger;

    public ConsoleSmsSender(ILogger<ConsoleSmsSender> logger)
    {
        _logger = logger;
    }

    public Task SendSmsAsync(string phoneNumber, string message)
    {
        _logger.LogInformation("--- SMS MESSAGE ---");
        _logger.LogInformation("To: {Phone}", phoneNumber);
        _logger.LogInformation("Message: {Message}", message);
        _logger.LogInformation("-------------------");
        return Task.CompletedTask;
    }
}
