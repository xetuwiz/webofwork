namespace WebOfWork.Api.Integrations.Sms.Strategies;

public class OtpSmsStrategy : ISmsMessageStrategy
{
    public string GenerateMessage(params object[] args)
    {
        var otp = args[0].ToString();
        return $"Your WebOfWork verification code is {otp}. Do not share this with anyone.";
    }
}
