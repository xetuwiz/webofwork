namespace WebOfWork.Api.DTOs.Notifications;

public record NotificationResponse
{
    public int Id { get; init; }
    public string? TargetRole { get; init; }
    public string Type { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public string? RelatedEntityType { get; init; }
    public int? RelatedEntityId { get; init; }
    public bool IsRead { get; init; }
    public DateTime CreatedAt { get; init; }
}

public record NotificationListResponse
{
    public int UnreadCount { get; init; }
    public int TotalCount { get; init; }
    public IReadOnlyList<NotificationResponse> Items { get; init; } = Array.Empty<NotificationResponse>();
}
