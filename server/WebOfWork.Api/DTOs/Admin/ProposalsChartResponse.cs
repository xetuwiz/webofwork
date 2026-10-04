namespace WebOfWork.Api.DTOs.Admin;

public class ProposalsChartResponse
{
    // AdminProposalsChart.jsx: dataKey="date" / "projects" / "proposals".
    public DateTime Date { get; set; }
    public int Projects { get; set; }
    public int Proposals { get; set; }

    // Retained extras from the previous shape.
    public int Submitted { get; set; }
    public int Accepted { get; set; }
    public int Rejected { get; set; }
}
