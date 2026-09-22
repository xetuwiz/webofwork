using System.Threading;
using Microsoft.Extensions.Logging;
using WebOfWork.Api.Integrations.Email;

namespace WebOfWork.Api.Jobs;

public class SendOtpEmailJob
{
    private readonly IEmailSender _emailSender;
    private readonly ILogger<SendOtpEmailJob> _logger;

    public SendOtpEmailJob(IEmailSender emailSender, ILogger<SendOtpEmailJob> logger)
    {
        _emailSender = emailSender;
        _logger = logger;
    }

    public async Task ExecuteAsync(string email, string otp, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(otp))
        {
            _logger.LogWarning("SendOtpEmailJob skipped due to missing email or OTP");
            return;
        }

        var html = $@"<h2>Welcome to WebOfWork!</h2>
<p>Your verification code is: <strong>{otp}</strong></p>
<p>This code expires in 10 minutes.</p>";

        await _emailSender.SendAsync(email, "Your WebOfWork OTP", html, ct);
    }
}
