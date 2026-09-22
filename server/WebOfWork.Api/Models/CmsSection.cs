using System;
using WebOfWork.Api.Common.Interfaces;

namespace WebOfWork.Api.Models;

public enum CmsSectionCategory
{
    Terms = 1,
    Privacy = 2
}

public class CmsSection : IAuditableEntity
{
    public int Id { get; set; }

    public CmsSectionCategory Category { get; set; }

    public string Heading { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
