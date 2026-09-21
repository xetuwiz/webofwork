namespace WebOfWork.Api.Integrations.Sms.Strategies;

public class PayoutReleasedSmsStrategy : ISmsMessageStrategy
{
    public string GenerateMessage(params object[] args)
    {
        var amount = args[0] is decimal decimalAmount
            ? decimalAmount.ToString("0.00")
            : args[0].ToString();
        return $"A payout of {amount} USD has been released to your account.";
    }
}
