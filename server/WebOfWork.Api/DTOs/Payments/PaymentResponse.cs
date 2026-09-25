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

    public WebOfWork.Api.Models.Enums.PaymentStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Envelope returned by the freelancer earnings route.
/// </summary>
public class FreelancerEarningsResponse
{
    public int Count { get; set; }
    public string? Next { get; set; }
    public string? Previous { get; set; }

    /// <summary>
    /// Object rather than array: EarningsOverview.jsx reads
    /// <c>results.total_earning</c> and then <c>results.contracts.map(...)</c>.
    /// </summary>
    public FreelancerEarningsSummary Results { get; set; } = new();
}

public class FreelancerEarningsSummary
{
    public decimal TotalEarning { get; set; }
    public int TotalProjects { get; set; }
    public List<FreelancerEarningContract> Contracts { get; set; } = new();
}

public class FreelancerEarningContract
{
    // Mirrors the reference ContractListSerializer, which is what
    // FreelancerTransactionSerializer embeds as `contracts`:
    // id, project_title, freelancer_name, status, total_amount,
    // client_signed_at, freelancer_earnings.
    public int Id { get; set; }
    public string ProjectTitle { get; set; } = string.Empty;
    public string FreelancerName { get; set; } = string.Empty;
    /// <summary>lowercase snake_case contract status, matched by the status badge.</summary>
    public string Status { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public DateTime ClientSignedAt { get; set; }

    /// <summary>
    /// The reference reads escrow_details.freelancer_share here and returns 0.00 when
    /// the contract has no escrow row -- a stored share, not TotalAmount minus
    /// the platform fee.
    /// </summary>
    public decimal FreelancerEarnings { get; set; }
}
