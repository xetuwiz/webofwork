namespace WebOfWork.Api.DTOs.Admin;

public class RevenueChartResponse
{
    public DateTime Date { get; set; }
    public decimal RevenueUsd { get; set; }
    public int TotalTransactions { get; set; }
}
