namespace WebOfWork.Api.DTOs.Admin;

public class ProposalsChartResponse
{
    public DateTime Date { get; set; }
    public int Submitted { get; set; }
    public int Accepted { get; set; }
    public int Rejected { get; set; }
}
