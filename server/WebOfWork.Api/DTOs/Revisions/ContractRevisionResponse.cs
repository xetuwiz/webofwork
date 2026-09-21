using System;
using WebOfWork.Api.Models.Enums;

namespace WebOfWork.Api.DTOs.Revisions;

public record ContractRevisionResponse(
    int Id,
    int ContractId,
    Guid UserId,
    string Reason,
    RevisionStatus Status,
    string? RejectionMessage,
    DateTime RequestedAt,
    DateTime? ResolvedAt
);
