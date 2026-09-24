using System.Threading;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using WebOfWork.Api.Integrations.Email;

namespace WebOfWork.Api.Jobs;

public class SendOtpEmailJob
{
    private readonly IEmailSender _emailSender;
    private readonly ILogger<SendOtpEmailJob> _logger;
    private readonly IEmailTemplateRenderer _templates;

    public SendOtpEmailJob(IEmailSender emailSender, ILogger<SendOtpEmailJob> logger, IEmailTemplateRenderer templates)
    {
        _emailSender = emailSender;
        _logger = logger;
        _templates = templates;
    }

    public async Task ExecuteAsync(string email, string otp, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(otp))
        {
            _logger.LogWarning("SendOtpEmailJob skipped due to missing email or OTP");
            return;
        }

        var html = await _templates.RenderAsync("OtpEmail.cshtml", new Dictionary<string, string> { ["Otp"] = otp }, ct);

        await _emailSender.SendAsync(email, "Your WebOfWork OTP", html, ct);
    }
}
