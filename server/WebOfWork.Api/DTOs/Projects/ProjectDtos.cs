using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WebOfWork.Api.DTOs.Projects;

/// <summary>
/// Reads project pricing_type as either a string ("fixed"/"range") or a number
/// (1/2) and normalises to the lowercase string the service expects. Any other
/// value throws, so a typo still surfaces as a 400 rather than silently
/// defaulting to fixed pricing.
/// </summary>
public class FlexiblePricingTypeConverter : JsonConverter<string?>
{
    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null) return null;

        if (reader.TokenType == JsonTokenType.String)
        {
            var s = reader.GetString();
            if (string.IsNullOrWhiteSpace(s)) return null;
            s = s.Trim().ToLowerInvariant();
            return s is "fixed" or "range"
                ? s
                : throw new JsonException($"Invalid pricing_type '{s}'. Expected 'fixed' or 'range'.");
        }

        if (reader.TokenType == JsonTokenType.Number)
        {
            return reader.GetInt32() switch
            {
                1 => "fixed",
                2 => "range",
                _ => throw new JsonException("Invalid pricing_type. Expected 1 (fixed) or 2 (range).")
            };
        }

        throw new JsonException("Invalid pricing_type. Expected 'fixed' or 'range'.");
    }

    public override void Write(Utf8JsonWriter writer, string? value, JsonSerializerOptions options)
    {
        if (value is null) { writer.WriteNullValue(); return; }
        writer.WriteStringValue(value);
    }
}

public class CreateProjectRequest
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    // Extended fields from reference: requirements + expected_deliverables
    public string Requirements { get; set; } = string.Empty;
    public string ExpectedDeliverables { get; set; } = string.Empty;

    // Accepts "fixed"/"range" (what the React form sends) as well as 1/2.
    // Plain `int` here broke model binding outright: the form posts a string, so
    // ASP.NET failed with "The JSON value could not be converted to System.Int32"
    // and returned 400 before any of the service's real validation ran.
    [JsonConverter(typeof(FlexiblePricingTypeConverter))]
    public string? PricingType { get; set; }
    public string PricingTypeName { get; set; } = "fixed";
    public int? Category { get; set; }
    public int? CategoryId { get; set; }
    // the frontend send min_budget/max_budget; MinPrice/MaxPrice kept as accepted aliases.
    public decimal? MinBudget { get; set; }
    public decimal? MaxBudget { get; set; }
    public decimal? MinPrice { get; set; }
    public decimal? MaxPrice { get; set; }
    public List<string> Skills { get; set; } = new();
    // the frontend send delivery_days; ExpectedDays is a legacy fallback.
    public int DeliveryDays { get; set; }
    public int ExpectedDays { get; set; }
    public decimal? FixedPrice { get; set; }

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
    // EditProject.jsx sends delivery_days; ExpectedDays is the legacy fallback.
    public int DeliveryDays { get; set; }
    public int ExpectedDays { get; set; }
    public int? CategoryId { get; set; }
    public List<int> SkillIds { get; set; } = new();
    public List<string> CustomSkillNames { get; set; } = new();

    // Pricing was previously absent here, so EditProject rendered Fixed Budget /
    // Min / Max, submitted them, got 200 back -- and the service silently
    // discarded every price. Editing a project's price appeared to work and did
    // not. Null means "leave unchanged", so a partial update stays partial.
    [JsonConverter(typeof(FlexiblePricingTypeConverter))]
    public string? PricingType { get; set; }
    public decimal? FixedPrice { get; set; }
    public decimal? MinBudget { get; set; }
    public decimal? MaxBudget { get; set; }
    // the frontend aliases, same as the create DTO.
    public decimal? MinPrice { get; set; }
    public decimal? MaxPrice { get; set; }
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
    public WebOfWork.Api.Models.Enums.ProjectPricingType PricingType { get; set; }
    public decimal? FixedPrice { get; set; }
    public decimal? MinBudget { get; set; }
    public decimal? MaxBudget { get; set; }
    public int DeliveryDays { get; set; }
    public WebOfWork.Api.Models.Enums.ProjectStatus Status { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    /// <summary>the reference ProjectListSerializer.get_skills returns a List[str] of names, and JobCard</summary>
    /// renders each entry directly — serialising objects here produced "[object Object]".</summary>
    public List<string> Skills { get; set; } = new();
    /// <summary>ProjectCard.jsx renders job.category_name; this was absent entirely.</summary>
    public string CategoryName { get; set; } = string.Empty;
    /// <summary>the reference ProjectListSerializer lists both category (id) and category_name.</summary>
    public int? Category { get; set; }
    public object? ClientInfo { get; set; }
    public bool IsApplied { get; set; }
    public int ProposalsCount { get; set; }
    public int? ContractId { get; set; }
    public int? WorkflowRunId { get; set; }
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
    /// <summary>
    /// DRF PageNumberPagination always emits next/previous (absolute URL, or null at the boundary).
    /// MyProjects.jsx gates its paginator on data?.next / data?.previous, so these must exist.
    /// </summary>
    public string? Next { get; set; }
    public string? Previous { get; set; }
}

public class ProjectCountResponse
{
    public int All { get; set; }
    public int Completed { get; set; }
    public int InProgress { get; set; }
    public int Open { get; set; }
}
