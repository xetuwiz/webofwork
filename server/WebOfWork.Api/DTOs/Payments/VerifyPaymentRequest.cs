namespace WebOfWork.Api.DTOs.Payments;

public class VerifyPaymentRequest
{
    public int ContractId { get; set; }
    public string PayPalOrderId { get; set; } = string.Empty;
    /// <summary>
    /// The USD amount that was actually charged via PayPal (PayPal processes USD).
    /// This is verified against the PayPal order. Internal contract amount remains in USD.
    /// </summary>
    public decimal PayPalAmountUsd { get; set; }
}
