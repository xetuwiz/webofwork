namespace WebOfWork.Api.Integrations.Sms.Strategies;

public class PaymentVerifiedSmsStrategy : ISmsMessageStrategy
{
    public string GenerateMessage(params object[] args)
    {
        var amount = args[0];
        var contractId = args[1];
        return "Payment of " + amount + " LKR has been verified and placed in escrow for Contract #" + contractId + ".";
    }
}
