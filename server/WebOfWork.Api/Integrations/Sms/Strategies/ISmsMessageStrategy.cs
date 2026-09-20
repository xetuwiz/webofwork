namespace WebOfWork.Api.Integrations.Sms.Strategies;

public interface ISmsMessageStrategy
{
    string GenerateMessage(params object[] args);
}
