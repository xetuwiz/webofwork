using Microsoft.EntityFrameworkCore;
using WebOfWork.Api.Data;
using WebOfWork.Api.DTOs.Chat;
using WebOfWork.Api.Models;
using WebOfWork.Api.Models.Enums;

namespace WebOfWork.Api.Services;

public interface IChatService
{
    Task<IReadOnlyList<ChatRoomResponse>> GetMyRoomsAsync(Guid userId, UserRole role, CancellationToken ct = default);
    Task<ChatRoomResponse> GetOrCreateRoomAsync(Guid userId, UserRole role, int contractId, CancellationToken ct = default);
    Task<ChatRoomDetailResponse> GetRoomDetailAsync(Guid userId, UserRole role, int roomId, CancellationToken ct = default);
    Task<MessageListResponse> GetRoomMessagesAsync(Guid userId, int roomId, int pageSize, int? beforeMessageId, CancellationToken ct = default);
    Task<ChatMessageResponse> SendMessageAsync(Guid userId, UserRole role, int roomId, string content, CancellationToken ct = default);
    Task<IReadOnlyList<ChatMessageResponse>> GetMessagesAsync(Guid userId, int roomId, int limit = 50, CancellationToken ct = default);
    Task DeleteMessageAsync(Guid userId, int roomId, int messageId, CancellationToken ct = default);
    Task ArchiveRoomAsync(Guid userId, UserRole role, int roomId, CancellationToken ct = default);
    Task<int> MarkRoomAsReadAsync(Guid userId, int roomId, int? messageId = null, CancellationToken ct = default);
    Task<bool> IsParticipantAsync(Guid userId, int roomId, CancellationToken ct = default);
}

public class ChatService : IChatService
{
    private static readonly ContractStatus[] ChatAllowedStatuses =
    {
        ContractStatus.Active,
        ContractStatus.Submitted,
        ContractStatus.Disputed
    };

    private readonly WebOfWorkDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly INotificationPublisher _notificationPublisher;
    private readonly IContactInformationPolicy _contactPolicy;

    public ChatService(
        WebOfWorkDbContext db,
        ICurrentUser currentUser,
        INotificationPublisher notificationPublisher,
        IContactInformationPolicy contactPolicy)
    {
        _db = db;
        _currentUser = currentUser;
        _notificationPublisher = notificationPublisher;
        _contactPolicy = contactPolicy;
    }

    public async Task<IReadOnlyList<ChatRoomResponse>> GetMyRoomsAsync(Guid userId, UserRole role, CancellationToken ct)
    {
        var query = role == UserRole.Client
            ? _db.ChatRooms.Where(r => r.ClientId == userId)
            : role == UserRole.Freelancer
                ? _db.ChatRooms.Where(r => r.FreelancerId == userId)
                : _db.ChatRooms.Where(r => r.ClientId == userId || r.FreelancerId == userId);

        var rooms = await query
            .AsNoTracking()
            .OrderByDescending(r => r.LastMessageAt ?? r.CreatedAt)
            .Select(r => new ChatRoomResponse
            {
                Id = r.Id,
                ContractId = r.ContractId,
                Client = new ChatParticipantDto
                {
                    Id = r.Client.Id,
                    Name = r.Client.FullName,
                    Role = nameof(UserRole.Client)
                },
                Freelancer = new ChatParticipantDto
                {
                    Id = r.Freelancer.Id,
                    Name = r.Freelancer.FullName,
                    Role = nameof(UserRole.Freelancer)
                },
                LastMessagePreview = r.Messages
                    .OrderByDescending(m => m.CreatedAt)
                    .Where(m => !m.IsDeleted)
                    .Select(m => m.Text)
                    .FirstOrDefault()!,
                LastMessageAt = r.LastMessageAt,
                UnreadCount = r.Messages.Count(m => !m.IsDeleted && !m.IsRead && m.SenderId != userId),
                IsArchived = role == UserRole.Client ? r.ClientArchived : r.FreelancerArchived,
                ContractStatus = r.Contract.Status.ToString(),
                CreatedAt = r.CreatedAt
            })
            .ToListAsync(ct);

        return rooms;
    }

    public async Task<ChatRoomResponse> GetOrCreateRoomAsync(Guid userId, UserRole role, int contractId, CancellationToken ct)
    {
        var contract = await _db.Contracts
            .AsNoTracking()
            .Include(c => c.Client)
            .Include(c => c.Freelancer)
            .FirstOrDefaultAsync(c => c.Id == contractId, ct)
            ?? throw new KeyNotFoundException($"Contract {contractId} not found.");

        var clientUserId = contract.Client.UserId;
        var freelancerUserId = contract.Freelancer.UserId;

        var isParticipant = role == UserRole.Admin ||
                            (role == UserRole.Client && clientUserId == userId) ||
                            (role == UserRole.Freelancer && freelancerUserId == userId);
        if (!isParticipant)
            throw new UnauthorizedAccessException("You are not a participant on this contract.");

        var existing = await _db.ChatRooms
            .FirstOrDefaultAsync(r => r.ContractId == contractId &&
                                      r.ClientId == clientUserId &&
                                      r.FreelancerId == freelancerUserId, ct);

        if (existing == null)
        {
            existing = new ChatRoom
            {
                ContractId = contractId,
                ClientId = clientUserId,
                FreelancerId = freelancerUserId
            };
            _db.ChatRooms.Add(existing);
            await _db.SaveChangesAsync(ct);
        }

        return await ProjectRoom(existing.Id, userId, role, ct);
    }

    public async Task<ChatRoomDetailResponse> GetRoomDetailAsync(Guid userId, UserRole role, int roomId, CancellationToken ct)
    {
        var room = await FindRoomOrThrow(userId, roomId, ct, role);
        var canSend = ChatAllowedStatuses.Contains(room.Contract.Status);

        var unreadCount = await _db.Messages
            .IgnoreQueryFilters()
            .CountAsync(m => m.RoomId == roomId && m.SenderId != userId && !m.IsRead && !m.IsDeleted, ct);

        return new ChatRoomDetailResponse
        {
            Id = room.Id,
            ContractId = room.ContractId,
            Client = new ChatParticipantDto
            {
                Id = room.Client.Id,
                Name = room.Client.FullName,
                Role = nameof(UserRole.Client)
            },
            Freelancer = new ChatParticipantDto
            {
                Id = room.Freelancer.Id,
                Name = room.Freelancer.FullName,
                Role = nameof(UserRole.Freelancer)
            },
            CanSendMessages = canSend,
            ContractStatus = room.Contract.Status.ToString(),
            LastMessageAt = room.LastMessageAt,
            CreatedAt = room.CreatedAt,
            UnreadCount = unreadCount
        };
    }

    public async Task<MessageListResponse> GetRoomMessagesAsync(Guid userId, int roomId, int pageSize, int? beforeMessageId, CancellationToken ct)
    {
        _ = await FindRoomOrThrow(userId, roomId, ct);
        if (pageSize <= 0) pageSize = 50;
        if (pageSize > 200) pageSize = 200;

        var baseQuery = _db.Messages
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(m => m.RoomId == roomId);

        if (beforeMessageId.HasValue)
            baseQuery = baseQuery.Where(m => m.Id < beforeMessageId.Value);

        var latestPage = await baseQuery
            .OrderByDescending(m => m.Id)
            .Take(pageSize + 1)
            .Include(m => m.Sender)
            .ToListAsync(ct);

        bool hasMore = latestPage.Count > pageSize;
        if (hasMore) latestPage.RemoveAt(latestPage.Count - 1);

        latestPage.Reverse();

        return new MessageListResponse
        {
            RoomId = roomId,
            TotalCount = await _db.Messages.IgnoreQueryFilters().CountAsync(m => m.RoomId == roomId, ct),
            Items = latestPage.Select(ProjectMessage).ToList(),
            HasMore = hasMore
        };
    }

    public async Task<IReadOnlyList<ChatMessageResponse>> GetMessagesAsync(Guid userId, int roomId, int limit = 50, CancellationToken ct = default)
    {
        var list = await GetRoomMessagesAsync(userId, roomId, limit, null, ct);
        return list.Items;
    }

    public async Task<ChatMessageResponse> SendMessageAsync(Guid userId, UserRole role, int roomId, string content, CancellationToken ct)
    {
        var room = await FindRoomOrThrow(userId, roomId, ct, role);
        if (!ChatAllowedStatuses.Contains(room.Contract.Status))
            throw new InvalidOperationException($"Chat is not available for contracts in status {room.Contract.Status}.");

        if (_contactPolicy.ContainsContactInfo(content))
            throw new InvalidOperationException("Message contains blocked contact information (phone, email, social, or link).");

        var message = new Message
        {
            RoomId = roomId,
            SenderId = userId,
            SenderRole = role,
            Text = content
        };

        room.LastMessageAt = DateTime.UtcNow;
        if (role == UserRole.Client) room.ClientArchived = false;
        else room.FreelancerArchived = false;

        _db.Messages.Add(message);
        await _db.SaveChangesAsync(ct);

        var senderName = room.ClientId == userId ? room.Client.FullName : room.Freelancer.FullName;
        var otherUserId = room.ClientId == userId ? room.FreelancerId : room.ClientId;
        await _notificationPublisher.PublishAsync(otherUserId, NotificationType.NewMessage,
            $"New message from {senderName}",
            relatedEntityType: "ChatRoom", relatedEntityId: room.Id, ct,
            senderName);

        return await ProjectMessageById(message.Id, ct);
    }

    public async Task DeleteMessageAsync(Guid userId, int roomId, int messageId, CancellationToken ct)
    {
        var room = await FindRoomOrThrow(userId, roomId, ct);
        var message = await _db.Messages
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(m => m.Id == messageId && m.RoomId == roomId, ct)
            ?? throw new KeyNotFoundException($"Message {messageId} not found in room {roomId}.");

        if (message.SenderId != userId)
            throw new UnauthorizedAccessException("Only the message sender can delete a message.");

        message.IsDeleted = true;
        message.DeletedAt = DateTime.UtcNow;
        message.DeletedByUserId = userId;
        message.Text = "[deleted]";
        await _db.SaveChangesAsync(ct);
    }

    public async Task ArchiveRoomAsync(Guid userId, UserRole role, int roomId, CancellationToken ct)
    {
        var room = await FindRoomOrThrow(userId, roomId, ct, role);
        if (role == UserRole.Client) room.ClientArchived = true;
        else if (role == UserRole.Freelancer) room.FreelancerArchived = true;
        else throw new NotSupportedException("Admin does not have an archive state.");
        await _db.SaveChangesAsync(ct);
    }

    public async Task<int> MarkRoomAsReadAsync(Guid userId, int roomId, int? messageId = null, CancellationToken ct = default)
    {
        _ = await FindRoomOrThrow(userId, roomId, ct);
        var query = _db.Messages
            .Where(m => m.RoomId == roomId && m.SenderId != userId && !m.IsRead && !m.IsDeleted);
        if (messageId.HasValue) query = query.Where(m => m.Id <= messageId.Value);

        var toUpdate = await query.ToListAsync(ct);
        foreach (var m in toUpdate) m.IsRead = true;
        if (toUpdate.Count > 0) await _db.SaveChangesAsync(ct);
        return toUpdate.Count;
    }

    public async Task<bool> IsParticipantAsync(Guid userId, int roomId, CancellationToken ct)
    {
        return await _db.ChatRooms
            .AnyAsync(r => r.Id == roomId && (r.ClientId == userId || r.FreelancerId == userId), ct);
    }

    private async Task<ChatRoom> FindRoomOrThrow(Guid userId, int roomId, CancellationToken ct, UserRole? role = null)
    {
        var room = await _db.ChatRooms
            .Include(r => r.Contract)
            .Include(r => r.Client)
            .Include(r => r.Freelancer)
            .FirstOrDefaultAsync(r => r.Id == roomId, ct)
            ?? throw new KeyNotFoundException($"Room {roomId} not found.");

        var isAdmin = role == UserRole.Admin;
        if (!isAdmin && room.ClientId != userId && room.FreelancerId != userId)
            throw new UnauthorizedAccessException("You are not a participant in this room.");
        return room;
    }

    private async Task<ChatRoomResponse> ProjectRoom(int roomId, Guid viewerId, UserRole viewerRole, CancellationToken ct)
    {
        var result = await _db.ChatRooms
            .AsNoTracking()
            .Where(r => r.Id == roomId)
            .Select(r => new ChatRoomResponse
            {
                Id = r.Id,
                ContractId = r.ContractId,
                Client = new ChatParticipantDto { Id = r.Client.Id, Name = r.Client.FullName, Role = nameof(UserRole.Client) },
                Freelancer = new ChatParticipantDto { Id = r.Freelancer.Id, Name = r.Freelancer.FullName, Role = nameof(UserRole.Freelancer) },
                LastMessagePreview = r.Messages
                    .OrderByDescending(m => m.CreatedAt)
                    .Where(m => !m.IsDeleted)
                    .Select(m => m.Text)
                    .FirstOrDefault()!,
                LastMessageAt = r.LastMessageAt,
                UnreadCount = r.Messages.Count(m => !m.IsDeleted && !m.IsRead && m.SenderId != viewerId),
                IsArchived = viewerRole == UserRole.Client ? r.ClientArchived : r.FreelancerArchived,
                ContractStatus = r.Contract.Status.ToString(),
                CreatedAt = r.CreatedAt
            })
            .FirstAsync(ct);
        return result;
    }

    private async Task<ChatMessageResponse> ProjectMessageById(int messageId, CancellationToken ct)
    {
        var m = await _db.Messages
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Include(x => x.Sender)
            .FirstAsync(x => x.Id == messageId, ct);
        return ProjectMessage(m);
    }

    private static ChatMessageResponse ProjectMessage(Message m)
    {
        return new ChatMessageResponse
        {
            Id = m.Id,
            RoomId = m.RoomId,
            SenderId = m.SenderId,
            SenderRole = m.SenderRole.ToString(),
            SenderName = m.Sender?.FullName ?? string.Empty,
            Text = m.Text,
            IsRead = m.IsRead,
            IsDeleted = m.IsDeleted,
            CreatedAt = m.CreatedAt
        };
    }
}
