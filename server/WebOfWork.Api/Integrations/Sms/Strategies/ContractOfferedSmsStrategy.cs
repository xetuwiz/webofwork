namespace WebOfWork.Api.Integrations.Sms.Strategies;

public class ContractOfferedSmsStrategy : ISmsMessageStrategy
{
    public string GenerateMessage(params object[] args)
    {
        if (args.Length > 0 && TryParseAmount(args[0], out var amount))
        {
            return $"You have received a new contract offer for {amount:0.00} USD on WebOfWork!";
        }

        return "You have received a new contract offer on WebOfWork!";
    }

    private static bool TryParseAmount(object value, out decimal amount)
    {
        if (value is decimal d)
        {
            amount = d;
            return true;
        }

        if (value is string s && decimal.TryParse(s, out var parsed))
        {
            amount = parsed;
            return true;
        }

        amount = 0m;
        return false;
    }
}
