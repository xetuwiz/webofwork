namespace WebOfWork.Api.DTOs.Projects;

public class CreateProjectRequest
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    // Extended fields from reference: requirements + expected_deliverables
    public string Requirements { get; set; } = string.Empty;
    public string ExpectedDeliverables { get; set; } = string.Empty;

    // 1 = Fixed, 2 = Range
    public int PricingType { get; set; }
    public decimal? FixedPrice { get; set; }
    public decimal? MinPrice { get; set; }
    public decimal? MaxPrice { get; set; }

    public int ExpectedDays { get; set; }

    // Optional category
    public int? CategoryId { get; set; }

    // Skill IDs (FK skills) OR custom skill names — mirrors ref CheckConstraint
    public List<int> SkillIds { get; set; } = new();
    public List<string> CustomSkillNames { get; set; } = new();
}

public class UpdateProjectRequest
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Requirements { get; set; } = string.Empty;
    public string ExpectedDeliverables { get; set; } = string.Empty;
    public int ExpectedDays { get; set; }
    public int? CategoryId { get; set; }
    public List<int> SkillIds { get; set; } = new();
    public List<string> CustomSkillNames { get; set; } = new();
}

public class ProjectResponse
{
    public int Id { get; set; }
    public Guid ClientId { get; set; }
    public int? CategoryId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Requirements { get; set; } = string.Empty;
    public string ExpectedDeliverables { get; set; } = string.Empty;
    public string PricingType { get; set; } = string.Empty;
    public decimal? FixedPrice { get; set; }
    public decimal? MinPrice { get; set; }
    public decimal? MaxPrice { get; set; }
    public int ExpectedDays { get; set; }
    public string Status { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public List<ProjectSkillResponse> Skills { get; set; } = new();
    public object? ClientInfo { get; set; }
    public bool IsApplied { get; set; }
    public int ProposalsCount { get; set; }
    public int? ContractId { get; set; }
}

public class ProjectSkillResponse
{
    public int? SkillId { get; set; }
    public string Name { get; set; } = string.Empty;  // Skill.Name or CustomName
}


public class ProjectListResponse
{
    public IReadOnlyList<ProjectResponse> Results { get; set; } = Array.Empty<ProjectResponse>();
    public int Count { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling(Count / (double)PageSize);
    public ProjectCountResponse Counts { get; set; } = new();
}

public class ProjectCountResponse
{
    public int All { get; set; }
    public int Completed { get; set; }
    public int InProgress { get; set; }
    public int Open { get; set; }
}
