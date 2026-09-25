namespace WebOfWork.Api.DTOs.Payments;

public class VerifyPaymentRequest
{
    public int ContractId { get; set; }
    public int ProjectId { get; set; }
    public Guid FreelancerId { get; set; }
    public decimal TotalAmount { get; set; }
    public string? ClientSignature { get; set; }
    public string? ClientSignatureType { get; set; }
    public string PayPalOrderId { get; set; } = string.Empty;
    public decimal PayPalAmountUsd { get; set; }
}
