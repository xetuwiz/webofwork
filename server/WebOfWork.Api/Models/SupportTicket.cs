using System;
using WebOfWork.Api.Common.Interfaces;
using WebOfWork.Api.Models.Enums;

namespace WebOfWork.Api.Models;

public enum SupportTicketStatus
{
    Open = 1,
    Resolved = 2,
    Cancelled = 3
}

public class SupportTicket : IAuditableEntity
{
    public int Id { get; set; }

    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public UserRole SenderRole { get; set; }

    public string Subject { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;

    public SupportTicketStatus Status { get; set; } = SupportTicketStatus.Open;

    public string? AdminReply { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public Guid? ResolvedByAdminId { get; set; }
    public User? ResolvedByAdmin { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
