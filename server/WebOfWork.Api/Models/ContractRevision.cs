using System;
using WebOfWork.Api.Models.Enums;

namespace WebOfWork.Api.Models;

public class ContractRevision
{
    public int Id { get; set; }
    
    public int ContractId { get; set; }
    public Contract Contract { get; set; } = null!;

    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public string Reason { get; set; } = string.Empty;
    public RevisionStatus Status { get; set; } = RevisionStatus.Pending;
    public string? RejectionMessage { get; set; }
    
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ResolvedAt { get; set; }
}
