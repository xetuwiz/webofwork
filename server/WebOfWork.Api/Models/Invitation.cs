using System;
using WebOfWork.Api.Models.Enums;

namespace WebOfWork.Api.Models;

public class Invitation
{
    public int Id { get; set; }

    public Guid ClientId { get; set; }
    public ClientProfile Client { get; set; } = null!;

    public Guid FreelancerId { get; set; }
    public FreelancerProfile Freelancer { get; set; } = null!;

    public int ProjectId { get; set; }
    public Project Project { get; set; } = null!;

    public Guid PackageId { get; set; }
    public FreelancerPackage Package { get; set; } = null!;

    public string? Message { get; set; }

    public InvitationStatus Status { get; set; } = InvitationStatus.Pending;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
