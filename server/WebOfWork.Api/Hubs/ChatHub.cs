using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using WebOfWork.Api.DTOs.Chat;
using WebOfWork.Api.Models.Enums;
using WebOfWork.Api.Services;

namespace WebOfWork.Api.Hubs;

[Authorize]
public class ChatHub : Hub
{
    private readonly IChatService _chat;
    private readonly ICurrentUser _currentUser;

    public ChatHub(IChatService chat, ICurrentUser currentUser)
    {
        _chat = chat;
        _currentUser = currentUser;
    }

    private Guid RequireUserId()
    {
        var id = _currentUser.Id;
        if (!id.HasValue) throw new HubException("Authenticated user id is missing.");
        return id.Value;
    }

    private UserRole RequireRole()
    {
        var roleClaim = _currentUser.Role;
        return Enum.TryParse<UserRole>(roleClaim, ignoreCase: true, out var r) ? r : UserRole.Client;
    }

    public override async Task OnConnectedAsync()
    {
        var uid = RequireUserId();
        await Groups.AddToGroupAsync(Context.ConnectionId, $"user_{uid:N}");
        await base.OnConnectedAsync();
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        return base.OnDisconnectedAsync(exception);
    }

    public async Task JoinRoom(int roomId)
    {
        var uid = RequireUserId();
        if (!await _chat.IsParticipantAsync(uid, roomId, Context.ConnectionAborted))
            throw new HubException("Not a participant of this room.");
        await Groups.AddToGroupAsync(Context.ConnectionId, $"room_{roomId}");
    }

    public async Task LeaveRoom(int roomId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"room_{roomId}");
    }

    public async Task Send(int roomId, string content)
    {
        var uid = RequireUserId();
        var role = RequireRole();

        ChatMessageResponse message;
        try
        {
            message = await _chat.SendMessageAsync(uid, role, roomId, content, Context.ConnectionAborted);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new HubException(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            throw new HubException(ex.Message);
        }

        await Clients.Group($"room_{roomId}").SendAsync("MessageReceived", message);
    }

    public async Task Typing(int roomId, bool isTyping)
    {
        var uid = RequireUserId();
        if (!await _chat.IsParticipantAsync(uid, roomId, Context.ConnectionAborted)) return;
        await Clients.OthersInGroup($"room_{roomId}").SendAsync("UserTyping", new { RoomId = roomId, UserId = uid, IsTyping = isTyping });
    }

    public async Task Read(int roomId, int? messageId = null)
    {
        var uid = RequireUserId();
        if (!await _chat.IsParticipantAsync(uid, roomId, Context.ConnectionAborted)) return;
        var marked = await _chat.MarkRoomAsReadAsync(uid, roomId, messageId, Context.ConnectionAborted);
        await Clients.Group($"room_{roomId}").SendAsync("MessagesRead", new { RoomId = roomId, ReaderId = uid, Marked = marked, UpToMessageId = messageId });
    }

    public async Task DeleteMessage(int roomId, int messageId)
    {
        var uid = RequireUserId();
        try
        {
            await _chat.DeleteMessageAsync(uid, roomId, messageId, Context.ConnectionAborted);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new HubException(ex.Message);
        }
        await Clients.Group($"room_{roomId}").SendAsync("MessageDeleted", new { RoomId = roomId, MessageId = messageId });
    }
}
