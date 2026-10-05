using System.Text.Json;
using System.Text.Json.Serialization;

namespace WebOfWork.Api.DTOs.Refunds;
public class RefundPaymentRequest
{
    public int PaymentId { get; set; }
    [JsonPropertyName("payment_id")]
    public JsonElement PaymentIdValue { get; set; }
    public string Reason { get; set; } = string.Empty;
    public decimal? Amount { get; set; }
}
