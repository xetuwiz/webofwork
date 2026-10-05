namespace WebOfWork.Api.DTOs.Payouts;
public class ReleasePayoutRequest { public int PaymentId { get; set; } public int ContractId { get; set; } }
public record PayoutResult(string Message, string BatchId, string PayoutStatus);
