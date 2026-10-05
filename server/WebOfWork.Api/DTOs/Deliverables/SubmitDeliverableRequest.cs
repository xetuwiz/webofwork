using System;

namespace WebOfWork.Api.DTOs.Deliverables;

public record SubmitDeliverableRequest(
    string Title,
    string? FileUrl,
    string? LinkUrl,
    string? Notes,
    string? DeliverableType = null
);

/// <summary>
/// Partial update for an existing deliverable. Null fields are left unchanged so
/// the client can PATCH just the title or just the notes.
/// </summary>
public record UpdateDeliverableRequest(
    string? Title,
    string? FileUrl,
    string? LinkUrl,
    string? Notes
);
