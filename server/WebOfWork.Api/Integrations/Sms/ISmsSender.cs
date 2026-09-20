using System.Threading.Tasks;

namespace WebOfWork.Api.Integrations.Sms;

public interface ISmsSender
{
    Task SendSmsAsync(string phoneNumber, string message);
}
