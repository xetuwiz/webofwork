using System.Threading;
using Microsoft.Extensions.Logging;
using WebOfWork.Api.Integrations.Email;

namespace WebOfWork.Api.Jobs;

public class SendPasswordResetEmailJob
{
    private readonly IEmailSender _emailSender;
    private readonly ILogger<SendPasswordResetEmailJob> _logger;

    public SendPasswordResetEmailJob(IEmailSender emailSender, ILogger<SendPasswordResetEmailJob> logger)
    {
        _emailSender = emailSender;
        _logger = logger;
    }

    public async Task ExecuteAsync(string email, string token, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(token))
        {
            _logger.LogWarning("SendPasswordResetEmailJob skipped due to missing email or token");
            return;
        }

        var html = $@"<h2>Password Reset</h2>
<p>Use the following token to reset your password:</p>
<p><strong>{token}</strong></p>";

        await _emailSender.SendAsync(email, "Password Reset Request", html, ct);
    }
}
