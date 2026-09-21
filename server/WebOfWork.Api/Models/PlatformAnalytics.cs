using System;
namespace WebOfWork.Api.Models;
public class PlatformAnalytics
{
    public int Id { get; set; }
    public decimal TotalVolumeLkr { get; set; }
    public decimal TotalProfitLkr { get; set; }
    public int CompletedContractsCount { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
