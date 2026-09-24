using System.Threading;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using WebOfWork.Api.Integrations.Email;

namespace WebOfWork.Api.Jobs;

public class SendPasswordResetEmailJob
{
    private readonly IEmailSender _emailSender;
    private readonly ILogger<SendPasswordResetEmailJob> _logger;
    private readonly IEmailTemplateRenderer _templates;

    public SendPasswordResetEmailJob(IEmailSender emailSender, ILogger<SendPasswordResetEmailJob> logger, IEmailTemplateRenderer templates)
    {
        _emailSender = emailSender;
        _logger = logger;
        _templates = templates;
    }

    public async Task ExecuteAsync(string email, string token, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(token))
        {
            _logger.LogWarning("SendPasswordResetEmailJob skipped due to missing email or token");
            return;
        }

        var html = await _templates.RenderAsync("PasswordResetEmail.cshtml", new Dictionary<string, string> { ["Token"] = token }, ct);

        await _emailSender.SendAsync(email, "Password Reset Request", html, ct);
    }
}
