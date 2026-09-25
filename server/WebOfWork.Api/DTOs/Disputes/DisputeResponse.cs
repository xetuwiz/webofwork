using System;
using System.Collections.Generic;
using WebOfWork.Api.Models.Enums;

namespace WebOfWork.Api.DTOs.Disputes;

public class DisputeResponse
{
    public int Id { get; set; }
    public int ContractId { get; set; }
    public int? PaymentId { get; set; }
    public Guid OpenerId { get; set; }
    /// <summary>Alias of OpenerId under the name the reference ContractDetail.jsx reads.</summary>
    public Guid RaisedById { get; set; }
    public string? OpenerRole { get; set; }
    public DisputeReason Reason { get; set; }
    public string Description { get; set; } = string.Empty;
    public DisputeStatus Status { get; set; }
    public string? AdminNotes { get; set; }
    public DisputeResolution? Resolution { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<string> EvidenceUrls { get; set; } = new();

    /// <summary>
    /// The client on the contract, by name. Populated by the admin list query
    /// only.
    /// </summary>
    /// <remarks>
    /// DisputeList.jsx has always rendered <c>dispute.client_name</c> and
    /// <c>dispute.freelancer_name</c>. Neither field existed on this DTO, so both
    /// cells silently rendered empty -- an admin could not tell whose dispute
    /// they were reviewing.
    /// </remarks>
    public string? ClientName { get; set; }

    /// <summary>The freelancer on the contract, by name. Admin list only.</summary>
    public string? FreelancerName { get; set; }
}