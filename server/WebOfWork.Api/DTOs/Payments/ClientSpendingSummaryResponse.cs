namespace WebOfWork.Api.DTOs.Payments;

/// <summary>
/// for GET /payments/client/spending-summary/.
/// Field names match SpendingSummary.jsx exactly:
///   total_spent, projects_completed, ongoing_projects, avg_per_project, recent_projects[]
/// where each recent project exposes id, project_title, freelancer_name, total_amount, status.
/// </summary>
public class ClientSpendingSummaryResponse
{
    public decimal TotalSpent { get; set; }
    public int ProjectsCompleted { get; set; }
    public int OngoingProjects { get; set; }
    public decimal AvgPerProject { get; set; }
    public IReadOnlyList<ClientSpendingProject> RecentProjects { get; set; } = Array.Empty<ClientSpendingProject>();
}

public class ClientSpendingProject
{
    public int Id { get; set; }
    public string ProjectTitle { get; set; } = string.Empty;
    public string FreelancerName { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    /// <summary>snake_case (e.g. "completed", "in_progress") to match the frontend's status checks.</summary>
    public WebOfWork.Api.Models.Enums.ContractStatus Status { get; set; }
}
