namespace WebOfWork.Api.DTOs.Admin;

public class RevenueChartResponse
{
    // AdminRevenueChart.jsx: <XAxis dataKey="month"> and <Area dataKey="revenue">.
    public string Month { get; set; } = string.Empty;
    public decimal Revenue { get; set; }

    // Retained extras from the previous shape.
    public DateTime Date { get; set; }
    public decimal RevenueUsd { get; set; }
    public int TotalTransactions { get; set; }
}
