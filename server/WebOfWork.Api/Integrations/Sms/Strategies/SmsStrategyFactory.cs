using System;
using System.Collections.Generic;

namespace WebOfWork.Api.Integrations.Sms.Strategies;

public class SmsStrategyFactory
{
    private readonly Dictionary<string, ISmsMessageStrategy> _strategies;

    public SmsStrategyFactory()
    {
        _strategies = new Dictionary<string, ISmsMessageStrategy>(StringComparer.OrdinalIgnoreCase)
        {
            { "otp", new OtpSmsStrategy() },
            { "contract_offered", new ContractOfferedSmsStrategy() },
            { "payment_verified", new PaymentVerifiedSmsStrategy() },
            { "payout_released", new PayoutReleasedSmsStrategy() }
        };
    }

    public ISmsMessageStrategy GetStrategy(string notificationType)
    {
        if (_strategies.TryGetValue(notificationType, out var strategy))
            return strategy;
            
        throw new ArgumentException($"Unknown notification type: {notificationType}");
    }
}
