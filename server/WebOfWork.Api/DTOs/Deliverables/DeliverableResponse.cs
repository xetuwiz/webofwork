using System;

namespace WebOfWork.Api.DTOs.Deliverables;

public record DeliverableResponse(
    int Id,
    int ContractId,
    Guid FreelancerId,
    string Title,
    string? FileUrl,
    string? LinkUrl,
    string? Notes,
    DateTime SubmittedAt
);
