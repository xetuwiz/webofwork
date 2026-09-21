using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using WebOfWork.Api.Configuration;

namespace WebOfWork.Api.Integrations.PayPal;

public interface IPayPalOrderVerifier
{
    Task<bool> VerifyOrderAsync(string orderId, decimal expectedAmountUsd);
}

public class PayPalOrderVerifier : IPayPalOrderVerifier
{
    private readonly IPayPalClient _payPalClient;
    private readonly PayPalOptions _opts;

    public PayPalOrderVerifier(IPayPalClient payPalClient, IOptions<PayPalOptions> opts)
    {
        _payPalClient = payPalClient;
        _opts         = opts.Value;
    }

    public async Task<bool> VerifyOrderAsync(string orderId, decimal expectedAmountUsd)
    {
        var order = await _payPalClient.GetOrderDetailsAsync(orderId);
        if (order == null)                        return false;
        if (order.Status   != "COMPLETED")        return false;
        if (order.Currency != _opts.Currency)     return false;  // "USD" — PayPal processes USD
        if (order.Amount   != expectedAmountUsd)  return false;
        return true;
    }
}
