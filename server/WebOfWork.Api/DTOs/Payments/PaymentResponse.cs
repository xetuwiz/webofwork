using System;
namespace WebOfWork.Api.DTOs.Payments;
public class PaymentResponse
{
    public int Id { get; set; }
    public int ContractId { get; set; }
    public string PayPalOrderId { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
