using System;
namespace WebOfWork.Api.DTOs.Payments;

public class PaymentResponse
{
    public int Id { get; set; }
    public int ContractId { get; set; }
    public string PayPalOrderId { get; set; } = string.Empty;
    public string? PayPalCaptureId { get; set; }
    public decimal FreelancerShare { get; set; }
    public decimal Amount { get; set; }
    public decimal? PlatformFee { get; set; }
    public decimal? FreelancerAmount { get; set; }
    public string? DisputeStatus { get; set; }
    public string? DisputeRaisedBy { get; set; }
    public string Gateway { get; set; } = "PayPal";

    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
