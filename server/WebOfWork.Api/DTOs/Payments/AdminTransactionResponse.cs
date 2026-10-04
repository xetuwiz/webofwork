using WebOfWork.Api.DTOs.Common;

namespace WebOfWork.Api.DTOs.Payments;

/// <summary>
/// for GET /payments/platform-transactions/.
/// AdminTransactions.jsx reads, per row: id, payment_id, client_name, freelancer_name,
/// amount_total, platform_fee, status, date — plus results/count/next/previous for the pager.
/// <see cref="PaymentResponse"/> does not expose any of the name/date fields, so the admin
/// screen needs this projection instead.
/// </summary>
public class AdminTransactionResponse
{
    public int Id { get; set; }
    public string PaymentId { get; set; } = string.Empty;
    public string ClientName { get; set; } = string.Empty;
    public string FreelancerName { get; set; } = string.Empty;
    public decimal AmountTotal { get; set; }
    public decimal PlatformFee { get; set; }
    /// <summary>snake_case (e.g. "completed", "refunded") to match the admin status badges.</summary>
    public WebOfWork.Api.Models.Enums.PaymentStatus Status { get; set; }
    public DateTime Date { get; set; }
}

public class PagedAdminTransactionsResponse : PagedResponse<AdminTransactionResponse>
{
}
