namespace WebOfWork.Api.DTOs.Admin;

public class DashboardStatsResponse
{
    public decimal TotalRevenueUsd { get; set; }
    public decimal TotalPlatformFeesUsd { get; set; }
    public decimal NetRevenueUsd { get; set; }
    public decimal TotalPendingPayoutsUsd { get; set; }
    public int PendingPayoutCount { get; set; }
    public int TotalUsers { get; set; }
    public int ActiveUsers { get; set; }
    public int TotalProjects { get; set; }
    public int OpenProjects { get; set; }
    public int CompletedContracts { get; set; }
}
