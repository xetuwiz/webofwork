using System;
using System.Threading;
using Microsoft.Extensions.Logging;
using WebOfWork.Api.Integrations.Sms;

namespace WebOfWork.Api.Jobs;

public class SendPhoneOtpJob
{
    private readonly ISmsSender _smsSender;
    private readonly ILogger<SendPhoneOtpJob> _logger;

    public SendPhoneOtpJob(ISmsSender smsSender, ILogger<SendPhoneOtpJob> logger)
    {
        _smsSender = smsSender;
        _logger = logger;
    }

    public async Task ExecuteAsync(string phoneNumber, string otp, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber) || string.IsNullOrWhiteSpace(otp))
        {
            _logger.LogWarning("SendPhoneOtpJob skipped due to missing phone number or OTP");
            return;
        }

        var normalizedPhone = AndroidSmsGatewaySmsSender.NormalizeSriLankanPhoneNumber(phoneNumber);
        var message = $"Your WebOfWork verification code is {otp}. It expires in 10 minutes.";

        await _smsSender.SendSmsAsync(normalizedPhone, message);
    }
}
