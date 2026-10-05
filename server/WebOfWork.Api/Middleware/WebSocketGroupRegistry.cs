using System.Collections.Concurrent;
using System.Net.WebSockets;

namespace WebOfWork.Api.Middleware;

public sealed class WebSocketGroupRegistry
{
    public ConcurrentDictionary<Guid, ConcurrentBag<WebSocket>> UserSockets { get; } = new();
    public ConcurrentDictionary<int, ConcurrentBag<WebSocket>> RoomSockets { get; } = new();
    // Which users currently hold at least one socket in a room. Kept separately from RoomSockets so
    // that every tab still receives broadcasts (a ConcurrentBag keeps them all), while HasUser can
    // answer "is this participant actually connected to this room?" for read-receipt gating.
    private readonly ConcurrentDictionary<int, ConcurrentDictionary<Guid, int>> _roomUsers = new();
    private readonly ConcurrentDictionary<WebSocket, int> _socketRoom = new();

    public void Add(Guid uid, WebSocket socket) => UserSockets.GetOrAdd(uid, _ => new()).Add(socket);
    public void Add(int roomId, WebSocket socket) => RoomSockets.GetOrAdd(roomId, _ => new()).Add(socket);
    public void Add(int roomId, Guid userId, WebSocket socket)
    {
        Add(roomId, socket);
        _roomUsers.GetOrAdd(roomId, _ => new())[userId] = 0;
        _socketRoom[socket] = roomId;
    }
    public void Remove(Guid uid, WebSocket socket) => RemoveFrom(UserSockets, uid, socket);
    public void Remove(int roomId, WebSocket socket) => RemoveFrom(RoomSockets, roomId, socket);
    public void Remove(int roomId, Guid userId, WebSocket socket)
    {
        Remove(roomId, socket);
        if (_socketRoom.TryRemove(socket, out var mapped) && mapped == roomId && _roomUsers.TryGetValue(roomId, out var users))
        {
            // Only drop the user's presence flag when their last socket in this room goes away.
            if (!RoomSockets.TryGetValue(roomId, out var remaining) || !remaining.Any(s => ReferenceEquals(s, socket)))
                users.TryRemove(userId, out _);
        }
        if (_roomUsers.TryGetValue(roomId, out var after) && after.IsEmpty) _roomUsers.TryRemove(roomId, out _);
    }
    public bool HasUser(int roomId, Guid userId) => _roomUsers.TryGetValue(roomId, out var users) && users.ContainsKey(userId);
    public IReadOnlyCollection<WebSocket> GetOthers(int roomId, WebSocket exclude) => RoomSockets.TryGetValue(roomId, out var sockets) ? sockets.Where(s => !ReferenceEquals(s, exclude)).ToArray() : Array.Empty<WebSocket>();
    public IReadOnlyCollection<WebSocket> GetRoom(int roomId) => RoomSockets.TryGetValue(roomId, out var sockets) ? sockets.ToArray() : Array.Empty<WebSocket>();
    public IReadOnlyCollection<WebSocket> GetUsers(Guid uid) => UserSockets.TryGetValue(uid, out var sockets) ? sockets.ToArray() : Array.Empty<WebSocket>();
    private static void RemoveFrom<TKey>(ConcurrentDictionary<TKey, ConcurrentBag<WebSocket>> groups, TKey key, WebSocket socket) where TKey : notnull
    {
        if (!groups.TryGetValue(key, out var sockets)) return;
        var remaining = sockets.Where(s => !ReferenceEquals(s, socket)).ToArray();
        groups.TryRemove(key, out _); foreach (var item in remaining) sockets.TryTake(out _);
    }
}
