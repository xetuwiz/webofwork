using System;
using WebOfWork.Api.Models.Enums;
namespace WebOfWork.Api.Models;

public class Payment
{
    public int Id { get; set; }
    public int ContractId { get; set; }
    public Contract Contract { get; set; } = null!;
    public string PayPalOrderId { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public decimal PlatformFee { get; set; }
    public string? PayPalCaptureId { get; set; }
    public decimal FreelancerShare { get; set; }
    public decimal RefundedAmount { get; set; }
    public ICollection<PaymentRefund> Refunds { get; set; } = new List<PaymentRefund>();

    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ProcessedAt { get; set; }
}
