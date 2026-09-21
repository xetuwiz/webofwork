using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebOfWork.Api.Services;

namespace WebOfWork.Api.Controllers;

[ApiController]
[Route("api/notifications")]
[Authorize]
public class NotificationsController : ControllerBase
{
    private readonly INotificationService _notifications;

    public NotificationsController(INotificationService notifications)
    {
        _notifications = notifications;
    }

    private Guid GetUserId()
    {
        var idClaim = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (!Guid.TryParse(idClaim, out var uid))
            throw new UnauthorizedAccessException("Missing valid user id claim.");
        return uid;
    }

    [HttpGet]
    public async Task<IActionResult> Get(
        [FromQuery] bool? isRead = null,
        [FromQuery] int limit = 50,
        CancellationToken ct = default)
    {
        var uid = GetUserId();
        var result = await _notifications.GetNotificationsAsync(uid, isRead, limit, ct);
        return Ok(new { data = result });
    }

    [HttpPatch("{id:int}/read")]
    public async Task<IActionResult> MarkRead(int id, CancellationToken ct)
    {
        var uid = GetUserId();
        try
        {
            var n = await _notifications.MarkAsReadAsync(uid, id, ct);
            return Ok(new { data = n });
        }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }

    [HttpPatch("read-all")]
    public async Task<IActionResult> MarkAllAsRead(CancellationToken ct)
    {
        var uid = GetUserId();
        var count = await _notifications.MarkAllAsReadAsync(uid, ct);
        return Ok(new { marked = count });
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var uid = GetUserId();
        try
        {
            await _notifications.DeleteNotificationAsync(uid, id, ct);
            return NoContent();
        }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }
}
