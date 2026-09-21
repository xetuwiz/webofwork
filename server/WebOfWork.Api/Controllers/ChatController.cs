using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebOfWork.Api.DTOs.Chat;
using WebOfWork.Api.Models.Enums;
using WebOfWork.Api.Services;

namespace WebOfWork.Api.Controllers;

[ApiController]
[Route("api/chat")]
[Authorize]
public class ChatController : ControllerBase
{
    private readonly IChatService _chat;
    private readonly IValidator<MessageRequest> _messageValidator;
    private readonly IValidator<GetOrCreateRoomRequest> _roomRequestValidator;

    public ChatController(
        IChatService chat,
        IValidator<MessageRequest> messageValidator,
        IValidator<GetOrCreateRoomRequest> roomRequestValidator)
    {
        _chat = chat;
        _messageValidator = messageValidator;
        _roomRequestValidator = roomRequestValidator;
    }

    private (Guid userId, UserRole role) GetUserContext()
    {
        var idClaim = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (!Guid.TryParse(idClaim, out var uid))
            throw new UnauthorizedAccessException("Missing valid user id claim.");
        var roleClaim = User.FindFirstValue("role") ?? User.FindFirstValue(ClaimTypes.Role);
        if (!Enum.TryParse<UserRole>(roleClaim, ignoreCase: true, out var role))
            role = UserRole.Client;
        return (uid, role);
    }

    [HttpGet("rooms")]
    public async Task<IActionResult> GetRooms(CancellationToken ct)
    {
        var (userId, role) = GetUserContext();
        var rooms = await _chat.GetMyRoomsAsync(userId, role, ct);
        return Ok(new { data = rooms });
    }

    [HttpPost("rooms")]
    public async Task<IActionResult> GetOrCreateRoom([FromBody] GetOrCreateRoomRequest request, CancellationToken ct)
    {
        var result = await _roomRequestValidator.ValidateAsync(request, ct);
        if (!result.IsValid) return BadRequest(new { errors = result.Errors });
        var (userId, role) = GetUserContext();
        try
        {
            var room = await _chat.GetOrCreateRoomAsync(userId, role, request.ContractId, ct);
            return Ok(new { data = room });
        }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }

    [HttpGet("rooms/{roomId:int}")]
    public async Task<IActionResult> GetRoomDetail(int roomId, CancellationToken ct)
    {
        var (userId, role) = GetUserContext();
        try
        {
            var detail = await _chat.GetRoomDetailAsync(userId, role, roomId, ct);
            return Ok(new { data = detail });
        }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }

    [HttpGet("rooms/{roomId:int}/messages")]
    public async Task<IActionResult> GetRoomMessages(
        int roomId,
        [FromQuery] int pageSize = 50,
        [FromQuery] int? beforeMessageId = null,
        CancellationToken ct = default)
    {
        var (userId, _) = GetUserContext();
        try
        {
            var page = await _chat.GetRoomMessagesAsync(userId, roomId, pageSize, beforeMessageId, ct);
            return Ok(new { data = page });
        }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }

    [HttpPost("rooms/{roomId:int}/messages")]
    public async Task<IActionResult> SendMessage(int roomId, [FromBody] MessageRequest request, CancellationToken ct)
    {
        var result = await _messageValidator.ValidateAsync(request, ct);
        if (!result.IsValid) return BadRequest(new { errors = result.Errors, blockedContactInfo = true });
        var (userId, role) = GetUserContext();
        try
        {
            var msg = await _chat.SendMessageAsync(userId, role, roomId, request.Content, ct);
            return Ok(new { data = msg });
        }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpDelete("rooms/{roomId:int}/messages/{messageId:int}")]
    public async Task<IActionResult> DeleteMessage(int roomId, int messageId, CancellationToken ct)
    {
        var (userId, _) = GetUserContext();
        try
        {
            await _chat.DeleteMessageAsync(userId, roomId, messageId, ct);
            return NoContent();
        }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }

    [HttpPatch("rooms/{roomId:int}/archive")]
    public async Task<IActionResult> ArchiveRoom(int roomId, CancellationToken ct)
    {
        var (userId, role) = GetUserContext();
        try
        {
            await _chat.ArchiveRoomAsync(userId, role, roomId, ct);
            return NoContent();
        }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (NotSupportedException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPatch("rooms/{roomId:int}/read")]
    public async Task<IActionResult> MarkRoomAsRead(int roomId, [FromBody] ReadReceiptRequest? request, CancellationToken ct)
    {
        var (userId, _) = GetUserContext();
        try
        {
            var count = await _chat.MarkRoomAsReadAsync(userId, roomId, request?.MessageId, ct);
            return Ok(new { marked = count });
        }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }
}
