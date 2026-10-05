namespace WebOfWork.Api.DTOs.Admin;

public class DashboardStatsResponse
{
    // ── Names read by AdminOverview.jsx (the reference AdminDashboardSerializer) ──
    public int TotalUsers { get; set; }
    public decimal UsersChange { get; set; }
    public int TotalProjects { get; set; }
    public decimal ProjectsChange { get; set; }
    public int ActiveContracts { get; set; }
    public decimal ContractsChange { get; set; }
    public int TotalProposals { get; set; }
    public decimal ProposalsChange { get; set; }
    public int TotalDisputes { get; set; }
    public decimal DisputeChange { get; set; }
    /// <summary>The platform's commission earnings, not gross volume.</summary>
    public decimal PlatformRevenue { get; set; }
    public decimal RevenueChange { get; set; }
    public decimal PendingWithdrawals { get; set; }
    public int PendingWithdrawalsCount { get; set; }

    // ── Retained extras; harmless for the client, useful elsewhere ──
    public decimal TotalRevenueUsd { get; set; }
    public decimal TotalPlatformFeesUsd { get; set; }
    public decimal NetRevenueUsd { get; set; }
    public decimal TotalPendingPayoutsUsd { get; set; }
    public int PendingPayoutCount { get; set; }
    public int ActiveUsers { get; set; }
    public int OpenProjects { get; set; }
    public int CompletedContracts { get; set; }
}
