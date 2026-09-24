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
    public string? OpenerRole { get; set; }
    public DisputeReason Reason { get; set; }
    public string Description { get; set; } = string.Empty;
    public DisputeStatus Status { get; set; }
    public string? AdminNotes { get; set; }
    public DisputeResolution? Resolution { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<string> EvidenceUrls { get; set; } = new();
}
