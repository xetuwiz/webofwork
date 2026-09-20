namespace WebOfWork.Api.Integrations.Sms.Strategies;

public class PayoutReleasedSmsStrategy : ISmsMessageStrategy
{
    public string GenerateMessage(params object[] args)
    {
        var amount = args[0].ToString();
        return $"A payout of {amount} LKR has been released to your account.";
    }
}
