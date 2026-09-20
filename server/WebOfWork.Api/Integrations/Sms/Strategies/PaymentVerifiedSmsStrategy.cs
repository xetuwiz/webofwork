namespace WebOfWork.Api.Integrations.Sms.Strategies;

public class PaymentVerifiedSmsStrategy : ISmsMessageStrategy
{
    public string GenerateMessage(params object[] args)
    {
        return "Your payment has been successfully verified.";
    }
}
