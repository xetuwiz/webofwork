namespace WebOfWork.Api.Integrations.Sms.Strategies;

public class ContractOfferedSmsStrategy : ISmsMessageStrategy
{
    public string GenerateMessage(params object[] args)
    {
        return "You have received a new contract offer on WebOfWork!";
    }
}
