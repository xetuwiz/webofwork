using System;
namespace WebOfWork.Api.Models;
public class PlatformAnalytics
{
    public int Id { get; set; }
    public decimal TotalVolumeUsd { get; set; }
    public decimal TotalProfitUsd { get; set; }
    public int CompletedContractsCount { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
