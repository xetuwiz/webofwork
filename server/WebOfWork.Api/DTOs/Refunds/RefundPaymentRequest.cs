namespace WebOfWork.Api.DTOs.Refunds;
public class RefundPaymentRequest
{
    public int PaymentId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public decimal? Amount { get; set; }
}
