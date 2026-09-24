using System;
using System.Collections.Generic;
using WebOfWork.Api.Models.Enums;

namespace WebOfWork.Api.Models;

public class Dispute
{
    public int Id { get; set; }

    public int ContractId { get; set; }
    public Contract Contract { get; set; } = null!;

    public Guid OpenerId { get; set; }
    public User Opener { get; set; } = null!;

    // Explicit participant identity retained alongside the legacy user id.
    // Exactly one is set for new disputes; nullable keeps existing rows compatible.
    public Guid? OpenedByClientId { get; set; }
    public ClientProfile? OpenedByClient { get; set; }
    public Guid? OpenedByFreelancerId { get; set; }
    public FreelancerProfile? OpenedByFreelancer { get; set; }

    public DisputeReason Reason { get; set; }
    public string Description { get; set; } = string.Empty;
    public DisputeStatus Status { get; set; } = DisputeStatus.Pending;

    public string? AdminNotes { get; set; }
    public DisputeResolution? Resolution { get; set; }

    public DateTime? ResolvedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<DisputeFile> Evidence { get; set; } = new List<DisputeFile>();
}
