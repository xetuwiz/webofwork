using System;
using WebOfWork.Api.Models;

namespace WebOfWork.Api.DTOs.Content;

public record FaqResponse
{
    public int Id { get; init; }
    public string Question { get; init; } = string.Empty;
    public string Answer { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public int SortOrder { get; init; }
    public DateTime CreatedAt { get; init; }
}

public record CmsSectionResponse
{
    public int Id { get; init; }
    public string Category { get; init; } = string.Empty;
    public string Heading { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public int SortOrder { get; init; }
}

public record UpsertFaqRequest
{
    public string Question { get; init; } = string.Empty;
    public string Answer { get; init; } = string.Empty;
    public FaqCategory Category { get; init; } = FaqCategory.General;
    public int SortOrder { get; init; }
    public bool IsActive { get; init; } = true;
}

public record UpsertCmsSectionRequest
{
    public CmsSectionCategory Category { get; init; }
    public string Heading { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public int SortOrder { get; init; }
    public bool IsActive { get; init; } = true;
}
