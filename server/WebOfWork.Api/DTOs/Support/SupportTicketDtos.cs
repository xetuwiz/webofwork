using System;
using WebOfWork.Api.Models.Enums;

namespace WebOfWork.Api.DTOs.Support;

public record CreateSupportTicketRequest
{
    public string Subject { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
}

public record AdminReplyRequest
{
    public string Reply { get; init; } = string.Empty;
}

public record SupportTicketResponse
{
    public int Id { get; init; }
    public Guid UserId { get; init; }
    public string? UserEmail { get; init; }
    public UserRole SenderRole { get; init; }
    public string Subject { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string? AdminReply { get; init; }
    public DateTime? ResolvedAt { get; init; }
    public Guid? ResolvedByAdminId { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}

public record SupportTicketListResponse
{
    public IReadOnlyList<SupportTicketResponse> Items { get; init; } = Array.Empty<SupportTicketResponse>();
    public int TotalCount { get; init; }
    public int OpenCount { get; init; }
}
