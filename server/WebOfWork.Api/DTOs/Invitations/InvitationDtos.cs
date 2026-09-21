using System;
using WebOfWork.Api.Models.Enums;

namespace WebOfWork.Api.DTOs.Invitations;

public class CreateInvitationRequest
{
    public Guid FreelancerId { get; set; }
    public int ProjectId { get; set; }
    public Guid PackageId { get; set; }
    public string? Message { get; set; }
}

public class UpdateInvitationStatusRequest
{
    public InvitationStatus Status { get; set; }
}

public class InvitationResponse
{
    public int Id { get; set; }
    public Guid ClientId { get; set; }
    public string ClientName { get; set; } = string.Empty;
    public Guid FreelancerId { get; set; }
    public string FreelancerName { get; set; } = string.Empty;
    public int ProjectId { get; set; }
    public string ProjectTitle { get; set; } = string.Empty;
    public Guid PackageId { get; set; }
    public string? Message { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
