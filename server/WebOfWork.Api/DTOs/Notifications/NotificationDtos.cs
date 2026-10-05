using System.Text.Json.Serialization;

namespace WebOfWork.Api.DTOs.Notifications;

public record NotificationResponse
{
    public int Id { get; init; }
    public string? TargetRole { get; init; }

    /// Serialized as `notification_type`, not `type`.
    ///
    /// The default snake_case policy turned this into `type`, which did not match
    /// the name the rest of the stack already used: the web client keys its icon map
    /// off `notification_type`, and the mobile socket handler detects a pushed
    /// notification with `frame.containsKey('notification_type')`. Because the
    /// WebSocket pushes this same projection, realtime notification events were
    /// never emitted on mobile. One name now serves REST, WebSocket, and both clients.
    [JsonPropertyName("notification_type")]
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
