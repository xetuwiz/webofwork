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

    /// <summary>
    /// The ticket author's display name.
    /// </summary>
    /// <remarks>
    /// Added because the admin endpoint was emitting the raw <c>UserId</c> under
    /// a key called <c>user_full_name</c>, and the admin UI renders that key as a
    /// person's name. The data was already loaded -- the query does
    /// <c>Include(t =&gt; t.User)</c> and <c>UserEmail</c> is populated from it --
    /// so nothing was missing except this one field being projected.
    /// </remarks>
    public string? UserFullName { get; init; }
    public UserRole SenderRole { get; init; }
    public string Subject { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public WebOfWork.Api.Models.SupportTicketStatus Status { get; init; }
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
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
}
