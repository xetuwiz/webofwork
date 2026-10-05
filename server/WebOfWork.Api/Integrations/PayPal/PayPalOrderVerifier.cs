using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
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
    private readonly ILogger<PayPalOrderVerifier> _logger;

    public PayPalOrderVerifier(IPayPalClient payPalClient, IOptions<PayPalOptions> opts, ILogger<PayPalOrderVerifier> logger)
    {
        _payPalClient = payPalClient;
        _opts         = opts.Value;
        _logger       = logger;
    }

    public async Task<bool> VerifyOrderAsync(string orderId, decimal expectedAmountUsd)
    {
        if (string.IsNullOrWhiteSpace(orderId))
{
            _logger.LogError("PayPal verification called with an EMPTY order id.");
            return false;
}

        var order = await _payPalClient.GetOrderDetailsAsync(orderId);
        if (order == null)
{
            // A failed lookup is NOT an amount mismatch. Splitting the two stops
            // an empty/invalid order id from being reported as a pricing problem.
            _logger.LogError("PayPal order {OrderId} could not be fetched.", orderId);
            return false;
        }
        if (order.Status != "COMPLETED")
{
            _logger.LogError("PayPal order {OrderId} status is {Status}, expected COMPLETED.", orderId, order.Status);
            return false;
        }
        if (order.Currency != _opts.Currency)
{
            _logger.LogError("PayPal order {OrderId} currency {Got}, expected {Want}.", orderId, order.Currency, _opts.Currency);
            return false;
        }
        if (order.Amount != expectedAmountUsd)
{
            _logger.LogError("PayPal order {OrderId} amount {Got}, expected {Want}.", orderId, order.Amount, expectedAmountUsd);
            return false;
        }
        return true;

    }
}
