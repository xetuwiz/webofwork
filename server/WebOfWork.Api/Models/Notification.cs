using System;
using WebOfWork.Api.Common.Interfaces;
using WebOfWork.Api.Models.Enums;

namespace WebOfWork.Api.Models;

public class Notification : IAuditableEntity
{
    public int Id { get; set; }

    public Guid RecipientId { get; set; }
    public User Recipient { get; set; } = null!;

    public NotificationType Type { get; set; }
    public string Message { get; set; } = string.Empty;

    public string? RelatedEntityType { get; set; }
    public int? RelatedEntityId { get; set; }

    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
