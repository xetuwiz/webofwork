using System;
using System.Collections.Generic;
using WebOfWork.Api.Models.Enums;

namespace WebOfWork.Api.Models;

public class Project
{
    public int Id { get; set; }
    public Guid ClientId { get; set; }
    public ClientProfile Client { get; set; } = null!;

    // Optional category link (mirrors ref: category FK on Project)
    public int? CategoryId { get; set; }
    public Category? Category { get; set; }

    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    // Extended detail fields from reference
    public string Requirements { get; set; } = string.Empty;
    public string ExpectedDeliverables { get; set; } = string.Empty;

    public ProjectPricingType PricingType { get; set; }
    public decimal? FixedPrice { get; set; }
    public decimal? MinPrice { get; set; }
    public decimal? MaxPrice { get; set; }

    public int ExpectedDays { get; set; }

    public ProjectStatus Status { get; set; } = ProjectStatus.Open;

    // Soft-active flag (mirrors ref: is_active = BooleanField(default=True))
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<ProjectSkill> Skills { get; set; } = new List<ProjectSkill>();
}

public class ProjectSkill
{
    public int Id { get; set; }
    public int ProjectId { get; set; }
    public Project Project { get; set; } = null!;

    // Either SkillId (FK) or CustomName, one must be set (mirrors ref CheckConstraint)
    public int? SkillId { get; set; }
    public Skill? Skill { get; set; }

    public string? CustomName { get; set; }
}
