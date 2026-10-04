using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebOfWork.Api.DTOs.Notifications;
using WebOfWork.Api.Models.Enums;
using WebOfWork.Api.Services;

namespace WebOfWork.Api.Controllers;

[ApiController]
[Route("api/notifications")]
[Authorize]
public class NotificationsController : ControllerBase
{
    private readonly INotificationService _notifications;
    private readonly INotificationPreferenceService _preferences;

    public NotificationsController(
        INotificationService notifications,
        INotificationPreferenceService preferences)
    {
        _notifications = notifications;
        _preferences = preferences;
    }

    /// Mirrors CommunicationController.Role(), which is where this logic already lived.
    /// Used to filter out notifications targeted at the other role.
    private UserRole? GetUserRole()
    {
        var roleClaim = User.FindFirstValue("role") ?? User.FindFirstValue(ClaimTypes.Role);
        return Enum.TryParse<UserRole>(roleClaim, true, out var role) ? role : null;
    }

    private Guid GetUserId()
    {
        var idClaim = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (!Guid.TryParse(idClaim, out var uid))
            throw new UnauthorizedAccessException("Missing valid user id claim.");
        return uid;
    }

    /// The single notification list for both clients.
    ///
    /// `isRead` is the original filter. `all` is accepted so the web client, which
    /// previously called the duplicate `/api/communication/notifications` route,
    /// keeps its `?all=false` -> unread-only meaning: `all=false` filters to unread,
    /// `all=true` applies no read filter.
    [HttpGet]
    public async Task<IActionResult> Get(
        [FromQuery] bool? isRead = null,
        [FromQuery] int limit = 50,
        [FromQuery] bool? all = null,
        CancellationToken ct = default)
    {
        var uid = GetUserId();
        if (all.HasValue && !isRead.HasValue)
        {
            isRead = all.Value ? null : false;
        }

        var result = await _notifications.GetNotificationsAsync(uid, isRead, limit, ct, GetUserRole());
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

    // ── Preferences ────────────────────────────────────────────────────────────
    //
    // Separate actions rather than extra parameters on `Get`, because a client
    // that only wants the list must not have to know a preferences object exists.

    /// <summary>
    /// Channel switches and the per-type, per-channel mute grid, scoped to the
    /// caller's role. Never 404s: a user with no saved row gets the defaults.
    /// </summary>
    /// <remarks>
    /// The role is required because SMS and push are freelancer-only and some
    /// types only apply to one role. Filtering here means a client never renders a
    /// switch the dispatcher would ignore.
    /// </remarks>
    [HttpGet("preferences")]
    public async Task<IActionResult> GetPreferences(CancellationToken ct)
    {
        var uid = GetUserId();
        var role = GetUserRole() ?? UserRole.Freelancer;
        return Ok(new { data = await _preferences.GetAsync(uid, role, ct) });
    }

    /// <summary>Partial update. Omitted fields are left alone.</summary>
    [HttpPut("preferences")]
    public async Task<IActionResult> UpdatePreferences(
        [FromBody] UpdateNotificationPreferenceRequest request,
        CancellationToken ct)
    {
        var uid = GetUserId();
        var role = GetUserRole() ?? UserRole.Freelancer;
        try
        {
            return Ok(new { data = await _preferences.UpdateAsync(uid, role, request, ct) });
        }
        catch (ArgumentException ex)
        {
            // An unknown type or channel in the mute list is a client bug, not a
            // server fault, and the message names the offenders so it is actionable.
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// The type and channel vocabulary for the caller's role, so a settings screen
    /// can render every control without hardcoding the list.
    /// </summary>
    [HttpGet("preferences/types")]
    public IActionResult GetPreferenceTypes()
    {
        var role = GetUserRole() ?? UserRole.Freelancer;
        return Ok(new
        {
            data = new
            {
                channels = WebOfWork.Api.Models.NotificationCatalogue.ChannelsFor(role)
                    .Select(c => new { key = WebOfWork.Api.Models.NotificationChannelNames.ToWire(c) }),
                types = WebOfWork.Api.Models.NotificationCatalogue.All
                    .Where(d => WebOfWork.Api.Models.NotificationCatalogue.AppliesToRole(d, role))
                    .Select(d => new { d.Type, d.Label, d.Description }),
            },
        });
    }

    // ── Devices (FCM) ───────────────────────────────────────────────────────────

    /// <summary>
    /// Registers this device for push. Idempotent on the token, because
    /// <c>onTokenRefresh</c> re-registers on every rotation and the app also
    /// re-registers on every cold start.
    /// </summary>
    [HttpPost("devices")]
    public async Task<IActionResult> RegisterDevice(
        [FromBody] RegisterDeviceRequest request,
        CancellationToken ct)
    {
        var uid = GetUserId();
        try
        {
            var result = await _preferences.RegisterDeviceAsync(uid, request, ct);
            return Ok(new { data = result.Device, created = result.Created });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("devices")]
    public async Task<IActionResult> ListDevices(CancellationToken ct) =>
        Ok(new { data = await _preferences.ListDevicesAsync(GetUserId(), ct) });

    /// <summary>Unregisters a device. Idempotent, so a repeated sign-out call is not an error.</summary>
    [HttpDelete("devices")]
    public async Task<IActionResult> UnregisterDevice(
        [FromQuery] string token,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(token))
            return BadRequest(new { error = "A device token is required." });

        var removed = await _preferences.UnregisterDeviceAsync(GetUserId(), token, ct);
        return removed ? NoContent() : NotFound(new { error = "That device is not registered." });
    }
}
