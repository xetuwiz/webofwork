using System;
using System.Collections.Generic;
using WebOfWork.Api.Common.Interfaces;

namespace WebOfWork.Api.Models;

public class ChatRoom : IAuditableEntity, ISoftDeletable
{
    public int Id { get; set; }

    public int ContractId { get; set; }
    public Contract Contract { get; set; } = null!;

    public Guid ClientId { get; set; }
    public User Client { get; set; } = null!;

    public Guid FreelancerId { get; set; }
    public User Freelancer { get; set; } = null!;

    public DateTime? LastMessageAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }

    public bool ClientArchived { get; set; }
    public bool FreelancerArchived { get; set; }

    public ICollection<Message> Messages { get; set; } = new List<Message>();
}
