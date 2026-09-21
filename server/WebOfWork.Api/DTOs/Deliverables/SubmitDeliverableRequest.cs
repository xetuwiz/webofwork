using System;

namespace WebOfWork.Api.DTOs.Deliverables;

public record SubmitDeliverableRequest(
    string Title,
    string? FileUrl,
    string? LinkUrl,
    string? Notes
);
