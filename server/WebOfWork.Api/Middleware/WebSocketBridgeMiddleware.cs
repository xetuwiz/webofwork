using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using WebOfWork.Api.Models.Enums;
using WebOfWork.Api.Models;
using WebOfWork.Api.Services;

namespace WebOfWork.Api.Middleware;

public sealed partial class WebSocketBridgeMiddleware
{
    private readonly RequestDelegate _next;
    private readonly WebSocketGroupRegistry _registry;
    public WebSocketBridgeMiddleware(RequestDelegate next, WebSocketGroupRegistry registry) { _next = next; _registry = registry; }
    [GeneratedRegex("^/ws/chat/(?<room>\\w+)/?$")] private static partial Regex ChatPath();

    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Path.StartsWithSegments("/ws") || !context.WebSockets.IsWebSocketRequest) { await _next(context); return; }
        var token = context.Request.Query["token"];
        if (token.Count == 0) token = context.Request.Query["access_token"];
        if (!string.IsNullOrWhiteSpace(token) && string.IsNullOrWhiteSpace(context.Request.Headers.Authorization)) context.Request.Headers.Authorization = $"Bearer {token}";
        // This bridge runs before UseAuthentication(), so context.User is still an empty principal here.
        // AuthenticateAsync returns the result rather than assigning context.User, so the result must be
        // consumed explicitly and its principal assigned before the IsAuthenticated check.
        var authResult = await context.AuthenticateAsync(JwtBearerDefaults.AuthenticationScheme);
        if (authResult.Principal == null || !authResult.Succeeded) { context.Response.StatusCode = 401; return; }
        context.User = authResult.Principal;
        if (context.User.Identity?.IsAuthenticated != true) { context.Response.StatusCode = 401; return; }
        var uid = Guid.Parse(context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
        var socket = await context.WebSockets.AcceptWebSocketAsync();
        var match = ChatPath().Match(context.Request.Path);
        if (match.Success) await HandleChat(context, socket, uid, match.Groups["room"].Value);
        else await HandleNotifications(context, socket, uid);
    }

    private async Task HandleChat(HttpContext context, WebSocket socket, Guid uid, string roomText)
    {
        if (!int.TryParse(roomText, out var roomId)) { await Send(socket, new { type = "error", message = "Invalid room" }); return; }
        using var scope = context.RequestServices.CreateScope();
        var chat = scope.ServiceProvider.GetRequiredService<IChatService>();
        var role = Enum.TryParse<UserRole>(context.User.FindFirst("role")?.Value ?? context.User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value, true, out var r) ? r : UserRole.Client;
        if (!await chat.IsParticipantAsync(uid, roomId)) { await Send(socket, new { type = "error", message = "Not a participant" }); return; }
        _registry.Add(roomId, uid, socket);
        try
        {
            await chat.MarkRoomAsReadAsync(uid, roomId);
            await Send(socket, new { type = "all_read_update" });
            var buffer = new byte[16 * 1024];
            while (socket.State == WebSocketState.Open)
            {
                var result = await socket.ReceiveAsync(buffer, CancellationToken.None);
                if (result.MessageType == WebSocketMessageType.Close) break;
                using var doc = JsonDocument.Parse(Encoding.UTF8.GetString(buffer, 0, result.Count));
                var data = doc.RootElement;
                var type = data.TryGetProperty("type", out var t) ? t.GetString() : null;
                if (type == "typing") { await Broadcast(roomId, socket, new { type = "typing", sender_id = uid, is_typing = data.GetProperty("is_typing").GetBoolean() }); continue; }
                if (type == "delete_message")
                {
                    var id = data.GetProperty("message_id").GetInt32();
                    try { await chat.DeleteMessageAsync(uid, roomId, id); await Broadcast(roomId, socket, new { type = "delete", message_id = id }); }
                    catch { await Send(socket, new { type = "error", message = "Message could not be deleted." }); }
                    continue;
                }
                if (type != "chat_message") continue;
                var message = data.GetProperty("message").GetString();
                if (CommunicationContactValidator.ContainsContactInfo(message)) { await Send(socket, new { type = "error", message = "Sharing contact info is prohibited." }); continue; }
                try
                {
                    var saved = await chat.SendMessageAsync(uid, role, roomId, message!);
                    await Broadcast(roomId, socket, new { type = "chat_message", message = new { id = saved.Id, text = saved.Text, sender_id = uid, sender_role = saved.SenderRole.ToLowerInvariant(), created_at = saved.CreatedAt } });
                    // The reference flips read on the recipient's consumer, not at send time. Gating on
                    // presence keeps "• Read" honest: it appears once the recipient actually has a
                    // live consumer in the room, and the mark still happens via the connect-time
                    // all_read_update path when they were offline at send time.
                    try
                    {
                        var otherUserId = await chat.GetOtherParticipantIdAsync(uid, roomId);
                        if (otherUserId.HasValue && _registry.HasUser(roomId, otherUserId.Value))
                        {
                            await chat.MarkRoomAsReadAsync(otherUserId.Value, roomId, saved.Id);
                            foreach (var peer in _registry.GetRoom(roomId))
                            {
                                try { await Send(peer, new { type = "read_receipt", message_id = saved.Id }); } catch { }
                            }
                        }
                    }
                    catch { /* receipt delivery is best-effort; the message itself is already persisted and broadcast */ }
                }
                catch { await Send(socket, new { type = "error", message = "Contract ended." }); }
            }
        }
        finally { _registry.Remove(roomId, uid, socket); }
    }

    private async Task HandleNotifications(HttpContext context, WebSocket socket, Guid uid)
    {
        // Pure push, matching the reference NotificationConsumer: rows are only delivered when
        // NotificationService.PublishAsync creates them and pushes to this same singleton registry.
        // The previous poll loop (seen = 0) replayed the newest 50 rows on every connect, which
        // re-fired the client's pop sound up to 50 times per mount/reconnect. Missed notifications
        // while offline are still recovered by the REST list, which the client fetches on mount.
        _registry.Add(uid, socket);
        try
        {
            var buffer = new byte[1024];
            while (socket.State == WebSocketState.Open)
            {
                var result = await socket.ReceiveAsync(buffer, CancellationToken.None);
                if (result.MessageType == WebSocketMessageType.Close) break;
            }
        }
        finally { _registry.Remove(uid, socket); }
    }

    private async Task Broadcast(int roomId, WebSocket sender, object value)
    {
        foreach (var socket in _registry.GetOthers(roomId, sender))
        {
            try { await Send(socket, value); } catch { }
        }
    }

    // Mirrors the REST serializer options in Program.cs so WebSocket payloads emit the same
    // snake_case / decimal-as-string / lowercase-enum / ISO-8601-Z shapes as the HTTP API.
    // JsonSerializer.Serialize with default options would otherwise emit raw PascalCase, bare
    // decimals and Kind-dependent DateTimes (no trailing "Z" for Npgsql's Unspecified kind).
    private static readonly JsonSerializerOptions WireJsonOptions = BuildWireJsonOptions();

    internal static JsonSerializerOptions BuildWireJsonOptions() => new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DictionaryKeyPolicy = JsonNamingPolicy.SnakeCaseLower,
        Converters =
        {
            new System.Text.Json.Serialization.JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower),
            new WebOfWork.Api.DecimalAsStringJsonConverter(),
            new WebOfWork.Api.IsoDateTimeJsonConverter()
        }
    };

    public static async Task Send(WebSocket socket, object value)
    {
        if (socket.State == WebSocketState.Open) await socket.SendAsync(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value, WireJsonOptions)), WebSocketMessageType.Text, true, CancellationToken.None);
    }
}
