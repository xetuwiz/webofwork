using System;
using WebOfWork.Api.Common.Interfaces;
using WebOfWork.Api.Models.Enums;

namespace WebOfWork.Api.Models;

public class Message : IAuditableEntity, ISoftDeletable
{
    public int Id { get; set; }

    public int RoomId { get; set; }
    public ChatRoom Room { get; set; } = null!;

    public Guid SenderId { get; set; }
    public User Sender { get; set; } = null!;

    public UserRole SenderRole { get; set; }
    public string Text { get; set; } = string.Empty;
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }

    public Guid? DeletedByUserId { get; set; }
}
