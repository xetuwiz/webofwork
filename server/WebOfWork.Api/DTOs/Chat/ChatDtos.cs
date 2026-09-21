namespace WebOfWork.Api.DTOs.Chat;

public record ChatParticipantDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Role { get; init; } = string.Empty;
}

public record ChatRoomResponse
{
    public int Id { get; init; }
    public int ContractId { get; init; }
    public ChatParticipantDto Client { get; init; } = new();
    public ChatParticipantDto Freelancer { get; init; } = new();
    public string? LastMessagePreview { get; init; }
    public DateTime? LastMessageAt { get; init; }
    public int UnreadCount { get; init; }
    public bool IsArchived { get; init; }
    public string ContractStatus { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
}

public record ChatRoomDetailResponse
{
    public int Id { get; init; }
    public int ContractId { get; init; }
    public ChatParticipantDto Client { get; init; } = new();
    public ChatParticipantDto Freelancer { get; init; } = new();
    public bool CanSendMessages { get; init; }
    public string ContractStatus { get; init; } = string.Empty;
    public DateTime? LastMessageAt { get; init; }
    public DateTime CreatedAt { get; init; }
    public int UnreadCount { get; init; }
}

public record ChatMessageResponse
{
    public int Id { get; init; }
    public int RoomId { get; init; }
    public Guid SenderId { get; init; }
    public string SenderRole { get; init; } = string.Empty;
    public string SenderName { get; init; } = string.Empty;
    public string Text { get; init; } = string.Empty;
    public bool IsRead { get; init; }
    public bool IsDeleted { get; init; }
    public DateTime CreatedAt { get; init; }
}

public record MessageRequest
{
    public string Content { get; init; } = string.Empty;
}

public record GetOrCreateRoomRequest
{
    public int ContractId { get; init; }
}

public record MessageListResponse
{
    public int RoomId { get; init; }
    public int TotalCount { get; init; }
    public IReadOnlyList<ChatMessageResponse> Items { get; init; } = Array.Empty<ChatMessageResponse>();
    public bool HasMore { get; init; }
}

public record ReadReceiptRequest
{
    public int? MessageId { get; init; }
}
