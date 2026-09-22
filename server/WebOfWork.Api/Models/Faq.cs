using System;
using WebOfWork.Api.Common.Interfaces;

namespace WebOfWork.Api.Models;

public enum FaqCategory
{
    General = 1,
    Freelancer = 2,
    Client = 3,
    Payment = 4
}

public class Faq : IAuditableEntity
{
    public int Id { get; set; }

    public string Question { get; set; } = string.Empty;
    public string Answer { get; set; } = string.Empty;
    public FaqCategory Category { get; set; } = FaqCategory.General;

    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
