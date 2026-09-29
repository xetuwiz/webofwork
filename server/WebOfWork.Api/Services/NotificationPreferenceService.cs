using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WebOfWork.Api.Data;
using WebOfWork.Api.DTOs.Notifications;
using WebOfWork.Api.Integrations.Push;
using WebOfWork.Api.Models;
using WebOfWork.Api.Models.Enums;

namespace WebOfWork.Api.Services;

/// <summary>
/// The per-channel answer for one (user, type) pair.
/// </summary>
/// <remarks>
/// A record rather than a tuple so a caller cannot silently read the fields in
/// the wrong order, and so adding a channel later is a compile error at every
/// construction site instead of a silent mismatch.
/// </remarks>
public readonly record struct ChannelResolution(bool InApp, bool Push, bool Sms, bool Email)
{
    public static ChannelResolution AllBlocked => new(false, false, false, false);

    public bool For(NotificationChannel channel) => channel switch
    {
        NotificationChannel.InApp => InApp,
        NotificationChannel.Push => Push,
        NotificationChannel.Sms => Sms,
        NotificationChannel.Email => Email,
        _ => false,
    };
}

public interface INotificationPreferenceService
{
    /// <summary>
    /// Current preferences, materialising the default row if the user has never
    /// saved any. Absence means "everything on", so a read never has to be
    /// special-cased by the caller.
    /// </summary>
    /// <summary>
    /// Current preferences, materialising the default row if the user has never
    /// saved any. Absence means "everything on", so a read never has to be
    /// special-cased by the caller.
    /// </summary>
    /// <param name="role">
    /// The caller's role. It decides which channels and which types the response
    /// contains, so a client is never shown a switch for a channel it cannot use
    /// (SMS and push are freelancer-only) or a type that can never reach it.
    /// </param>
    Task<NotificationPreferenceResponse> GetAsync(Guid userId, UserRole role, CancellationToken ct = default);

    /// <summary>Applies a partial update. Null fields are left unchanged.</summary>
    Task<NotificationPreferenceResponse> UpdateAsync(
        Guid userId, UserRole role, UpdateNotificationPreferenceRequest request, CancellationToken ct = default);

    /// <summary>Resolves whether a type may be delivered over in-app / push / SMS / email.</summary>
    /// <summary>
    /// Resolves every channel for one (user, type) pair.
    /// </summary>
    /// <remarks>
    /// Each channel is answered independently. The previous shape folded them into
    /// one tuple with a single `isMuted` factor, so a caller could not tell "this
    /// type is muted" from "this whole channel is off" -- which is how push ended
    /// up gated on the in-app switch, and how a user who turned in-app off
    /// silently lost push as well.
    /// </remarks>
    Task<ChannelResolution> ResolveChannelsAsync(
        Guid userId, string wireType, UserRole role, CancellationToken ct = default);

    /// <summary>Convenience for a dispatcher that only asks about one channel.</summary>
    Task<bool> IsChannelEnabledAsync(
        Guid userId, string wireType, NotificationChannel channel, UserRole role, CancellationToken ct = default);

    Task<RegisterDeviceResult> RegisterDeviceAsync(
        Guid userId, RegisterDeviceRequest request, CancellationToken ct = default);

    Task<IReadOnlyList<DeviceResponse>> ListDevicesAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Removes a device. Idempotent -- unregistering twice is not an error.</summary>
    Task<bool> UnregisterDeviceAsync(Guid userId, string token, CancellationToken ct = default);

    /// <summary>
    /// Delivers a push to every registered device of <paramref name="userId"/>,
    /// honouring their push switch and per-type mutes. Called after the row is
    /// written; failures are logged, never thrown.
    /// </summary>
    Task DispatchPushAsync(Notification notification, UserRole role, CancellationToken ct = default);
}

public sealed record RegisterDeviceResult(DeviceResponse Device, bool Created);

public class NotificationPreferenceService : INotificationPreferenceService
{
    private readonly WebOfWorkDbContext _db;
    private readonly IPushSender _push;
    private readonly ILogger<NotificationPreferenceService> _logger;

    public NotificationPreferenceService(
        WebOfWorkDbContext db,
        IPushSender push,
        ILogger<NotificationPreferenceService> logger)
    {
        _db = db;
        _push = push;
        _logger = logger;
    }

    public async Task<NotificationPreferenceResponse> GetAsync(Guid userId, UserRole role, CancellationToken ct = default)
    {
        var preference = await _db.NotificationPreferences
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserId == userId, ct);

        // Read as (type, channel) pairs now. A mute with a blank channel predates
        // the column and meant "everywhere", so it is expanded across the
        // channels that type actually supports rather than narrowed to in-app --
        // the user asked not to hear about this, and quietly restoring some
        // channels behind their back is the worse failure.
        var rows = await _db.NotificationTypePreferences
            .AsNoTracking()
            .Where(t => t.UserId == userId)
            .Select(t => new { t.Type, t.Channel })
            .ToListAsync(ct);

        var muted = new List<NotificationMuteDto>();
        foreach (var row in rows)
        {
            var descriptor = NotificationCatalogue.Find(row.Type);
            if (descriptor is null) continue; // unknown type: leave it, do not render it

            if (string.IsNullOrWhiteSpace(row.Channel))
            {
                muted.AddRange(descriptor.Channels
                    .Where(c => NotificationCatalogue.ChannelAvailableTo(role, c))
                    .Select(c => new NotificationMuteDto(row.Type, NotificationChannelNames.ToWire(c))));
            }
            else
            {
                muted.Add(new NotificationMuteDto(row.Type, row.Channel.ToLowerInvariant()));
            }
        }

        return Project(preference, muted, role);
    }

    public async Task<NotificationPreferenceResponse> UpdateAsync(
        Guid userId, UserRole role, UpdateNotificationPreferenceRequest request, CancellationToken ct = default)
    {
        var preference = await _db.NotificationPreferences
            .FirstOrDefaultAsync(p => p.UserId == userId, ct);

        if (preference is null)
        {
            preference = new NotificationPreference { UserId = userId };
            _db.NotificationPreferences.Add(preference);
        }

        if (request.MasterEnabled.HasValue) preference.MasterEnabled = request.MasterEnabled.Value;
        if (request.InAppEnabled.HasValue) preference.InAppEnabled = request.InAppEnabled.Value;
        if (request.PushEnabled.HasValue) preference.PushEnabled = request.PushEnabled.Value;
        if (request.SmsEnabled.HasValue) preference.SmsEnabled = request.SmsEnabled.Value;
        if (request.EmailEnabled.HasValue) preference.EmailEnabled = request.EmailEnabled.Value;
        preference.UpdatedAt = DateTime.UtcNow;

        // `Muted` (pairs) wins over `MutedTypes` (flat) when both are sent: a newer
        // client sends both for back-compat, and the flat list is the coarser of
        // the two, so honouring it would undo the per-channel choices.
        if (request.Muted is not null || request.MutedTypes is not null)
        {
            var pairs = NormaliseMutes(request, role);

            // Validated against the catalogue rather than stored blindly. A typo
            // would otherwise create a row that can never match a published type,
            // and the user would see a toggle that silently does nothing.
            var unknownTypes = pairs.Select(p => p.Type)
                .Where(t => !NotificationCatalogue.IsKnownWireValue(t))
                .Distinct(StringComparer.Ordinal).ToList();
            if (unknownTypes.Count > 0)
                throw new ArgumentException($"Unknown notification type(s): {string.Join(", ", unknownTypes)}.");

            var unknownChannels = pairs.Select(p => p.Channel)
                .Where(c => !NotificationChannelNames.IsKnown(c))
                .Distinct(StringComparer.Ordinal).ToList();
            if (unknownChannels.Count > 0)
                throw new ArgumentException($"Unknown notification channel(s): {string.Join(", ", unknownChannels)}.");

            // A mute for a channel the type cannot use, or the role cannot have, is
            // dropped rather than stored. Storing it would mean the dispatcher's
            // `allowed.Contains(channel)` check ignores it forever, and the user
            // would have a toggle that shows as off while delivery still happens.
            var usable = new List<(string Type, string Channel)>();
            foreach (var pair in pairs)
            {
                var descriptor = NotificationCatalogue.Find(pair.Type);
                if (descriptor is null) continue;

                var channel = NotificationChannelNames.Parse(pair.Channel);
                if (!NotificationCatalogue.Supports(descriptor, channel)) continue;
                if (!NotificationCatalogue.AppliesToRole(descriptor, role)) continue;
                if (!NotificationCatalogue.ChannelAvailableTo(role, channel)) continue;

                var candidate = (pair.Type, pair.Channel);
                if (!usable.Contains(candidate)) usable.Add(candidate);
            }

            var existing = await _db.NotificationTypePreferences
                .Where(t => t.UserId == userId)
                .ToListAsync(ct);
            _db.NotificationTypePreferences.RemoveRange(existing);

            foreach (var (type, channel) in usable)
                _db.NotificationTypePreferences.Add(new NotificationTypePreference
                {
                    UserId = userId,
                    Type = type,
                    Channel = channel,
                });
        }

        await _db.SaveChangesAsync(ct);
        return await GetAsync(userId, role, ct);
    }

    /// <summary>
    /// Folds both request shapes into (type, channel) pairs.
    /// </summary>
    /// <remarks>
    /// The legacy flat form meant "mute on every channel", and is expanded that way
    /// rather than being treated as an in-app-only mute -- narrowing it would hand
    /// a client that only knows the old shape a surprise.
    /// </remarks>
    private static List<(string Type, string Channel)> NormaliseMutes(
        UpdateNotificationPreferenceRequest request, UserRole role)
    {
        var result = new List<(string, string)>();

        if (request.Muted is not null)
        {
            foreach (var m in request.Muted.Where(m => m is not null))
            {
                if (string.IsNullOrWhiteSpace(m.Type)) continue;
                result.Add((m.Type.Trim(), (m.Channel ?? "").Trim()));
            }
        }

        if (request.MutedTypes is not null)
        {
            foreach (var t in request.MutedTypes.Where(t => !string.IsNullOrWhiteSpace(t)))
            {
                var descriptor = NotificationCatalogue.Find(t.Trim());
                var channels = descriptor is null
                    ? new[] { NotificationChannelNames.InApp }
                    : descriptor.Channels
                        .Where(c => NotificationCatalogue.ChannelAvailableTo(role, c))
                        .Select(NotificationChannelNames.ToWire);
                foreach (var c in channels) result.Add((t.Trim(), c));
            }
        }

        return result;
    }

    public async Task<ChannelResolution> ResolveChannelsAsync(
        Guid userId, string wireType, UserRole role, CancellationToken ct = default)
    {
        // 1. The type has to be allowed over that channel at all, regardless of
        //    this user. This is the gate that stops a "chat over email" request,
        //    and it is why the settings screen and the dispatcher agree.
        var descriptor = NotificationCatalogue.Find(wireType);
        if (descriptor is null) return ChannelResolution.AllBlocked;

        // 2. The role has to be able to use that channel (SMS/push are
        //    freelancer-only), and the type has to apply to the role.
        if (!NotificationCatalogue.AppliesToRole(descriptor, role)) return ChannelResolution.AllBlocked;

        var allowed = new HashSet<NotificationChannel>(descriptor.Channels);
        var preference = await _db.NotificationPreferences
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserId == userId, ct);

        bool On(NotificationChannel c) =>
            allowed.Contains(c)
            && NotificationCatalogue.ChannelAvailableTo(role, c)
            // No row means "everything on", which is what makes the migration safe:
            // existing users keep their behaviour without being backfilled.
            && (preference is null
                ? NotificationCatalogue.DefaultFor(c)
                : preference.MasterEnabled && preference.EnabledFor(c));

        // 3. Per-type, per-channel mutes. One indexed lookup for the type.
        var mutes = await _db.NotificationTypePreferences
            .AsNoTracking()
            .Where(t => t.UserId == userId && t.Type == wireType)
            .Select(t => t.Channel)
            .ToListAsync(ct);
        var muted = new HashSet<string>(mutes, StringComparer.OrdinalIgnoreCase);

        bool Ok(NotificationChannel c) => On(c) && !muted.Contains(NotificationChannelNames.ToWire(c));

        return new ChannelResolution(Ok(NotificationChannel.InApp), Ok(NotificationChannel.Push),
                                     Ok(NotificationChannel.Sms), Ok(NotificationChannel.Email));
    }

    public async Task<bool> IsChannelEnabledAsync(
        Guid userId, string wireType, NotificationChannel channel, UserRole role, CancellationToken ct = default)
    {
        var all = await ResolveChannelsAsync(userId, wireType, role, ct);
        return channel switch
        {
            NotificationChannel.InApp => all.InApp,
            NotificationChannel.Push => all.Push,
            NotificationChannel.Sms => all.Sms,
            NotificationChannel.Email => all.Email,
            _ => false,
        };
    }

    public async Task<RegisterDeviceResult> RegisterDeviceAsync(
        Guid userId, RegisterDeviceRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Token))
            throw new ArgumentException("A device token is required.");

        var token = request.Token.Trim();
        var platform = string.IsNullOrWhiteSpace(request.Platform) ? "android" : request.Platform.Trim().ToLowerInvariant();

        var existing = await _db.DeviceRegistrations
            .FirstOrDefaultAsync(d => d.Token == token, ct);

        bool created;
        if (existing is null)
        {
            existing = new DeviceRegistration { Token = token };
            _db.DeviceRegistrations.Add(existing);
            created = true;
        }
        else
        {
            created = false;
        }

        // Reassigning to the current user is deliberate: a shared device that
        // changed hands must stop delivering the previous account's pushes. The
        // unique index on (UserId, Token) does not prevent this, because the token
        // row itself is being moved rather than duplicated.
        existing.UserId = userId;
        existing.Platform = platform;
        existing.DeviceName = string.IsNullOrWhiteSpace(request.DeviceName)
            ? existing.DeviceName
            : request.DeviceName.Trim();
        existing.LastSeenAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        return new RegisterDeviceResult(
            new DeviceResponse(existing.Token, existing.Platform, existing.DeviceName, existing.LastSeenAt),
            created);
    }

    public async Task<IReadOnlyList<DeviceResponse>> ListDevicesAsync(
        Guid userId, CancellationToken ct = default) =>
        await _db.DeviceRegistrations
            .AsNoTracking()
            .Where(d => d.UserId == userId)
            .OrderByDescending(d => d.LastSeenAt)
            .Select(d => new DeviceResponse(d.Token, d.Platform, d.DeviceName, d.LastSeenAt))
            .ToListAsync(ct);

    public async Task<bool> UnregisterDeviceAsync(
        Guid userId, string token, CancellationToken ct = default)
    {
        var device = await _db.DeviceRegistrations
            .FirstOrDefaultAsync(d => d.UserId == userId && d.Token == token, ct);
        if (device is null) return false;

        _db.DeviceRegistrations.Remove(device);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task DispatchPushAsync(Notification notification, UserRole role, CancellationToken ct = default)
    {
        if (!_push.IsConfigured) return;

        var wireType = NotificationWireFormat.ToWireFormat(notification.Type);

        // Ask about PUSH, and only push.
        //
        // This used to read `if (!push || !inApp) return;` -- gating the push on
        // the in-app switch. That was wrong twice over: a user who turned off the
        // in-app tray silently lost push too, and the mute it appeared to be
        // honouring is now per-channel and is already reflected in the `push`
        // answer. The `!push` check alone is the whole condition.
        //
        // The role is a parameter rather than re-read from `notification`, so the
        // caller cannot pass a recipient's role and have it disagree with the one
        // used to resolve the other channels moments earlier.
        if (!await IsChannelEnabledAsync(
                notification.RecipientId, wireType, NotificationChannel.Push, role, ct))
        {
            return;
        }

        var devices = await _db.DeviceRegistrations
            .AsNoTracking()
            .Where(d => d.UserId == notification.RecipientId)
            .Select(d => d.Token)
            .ToListAsync(ct);

        if (devices.Count == 0) return;

        var data = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["notification_id"] = notification.Id.ToString(),
            ["type"] = wireType,
        };
        if (notification.RelatedEntityType is not null)
            data["related_entity_type"] = notification.RelatedEntityType;
        if (notification.RelatedEntityId.HasValue)
            data["related_entity_id"] = notification.RelatedEntityId.Value.ToString();

        var route = RouteFor(notification);
        // Also duplicated into the data payload, not just the `_click_action`
        // alias: the client's tap handler reads `route` out of the FCM data map
        // for the `onMessageOpenedApp` path, where `_click_action` is not what it
        // gets handed.
        data["route"] = route;

        var messages = devices
            .Select(token => new PushMessage(
                token,
                TitleFor(notification.Type),
                notification.Message,
                data,
                route))
            .ToList();

        try
        {
            var results = await _push.SendAsync(messages, ct);

            // A token FCM reports as permanently dead will fail on every future
            // notification. Pruning here is what stops the table from growing
            // without bound as users reinstall.
            var dead = results.Where(r => r.Invalidated).Select(r => r.Token).ToList();
            if (dead.Count > 0)
            {
                var rows = await _db.DeviceRegistrations
                    .Where(d => dead.Contains(d.Token))
                    .ToListAsync(ct);
                if (rows.Count > 0)
                {
                    _db.DeviceRegistrations.RemoveRange(rows);
                    await _db.SaveChangesAsync(ct);
                    _logger.LogInformation("Pruned {Count} dead FCM token(s).", rows.Count);
                }
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // The row is already written; a push failure must not fail the publish.
            _logger.LogError(ex, "FCM dispatch failed for notification {NotificationId}", notification.Id);
        }
    }

    /// <summary>Short shade title per type. Falls back to the brand name.</summary>
    private static string TitleFor(Models.Enums.NotificationType type) => type switch
    {
        Models.Enums.NotificationType.ContractStarted => "Contract started",
        Models.Enums.NotificationType.WorkDelivered => "Work delivered",
        Models.Enums.NotificationType.ContractCompleted => "Contract completed",
        Models.Enums.NotificationType.PaymentCredited => "Payment received",
        Models.Enums.NotificationType.PayoutReleased => "Payout released",
        Models.Enums.NotificationType.DisputeOpened => "Dispute opened",
        Models.Enums.NotificationType.DisputeResolved => "Dispute resolved",
        Models.Enums.NotificationType.RevisionRequested => "Changes requested",
        Models.Enums.NotificationType.RevisionAccepted => "Changes accepted",
        Models.Enums.NotificationType.RevisionRejected => "Changes rejected",
        Models.Enums.NotificationType.OfferReceived => "New offer",
        Models.Enums.NotificationType.InvitationReceived => "New invitation",
        Models.Enums.NotificationType.ProposalAccepted => "Proposal accepted",
        Models.Enums.NotificationType.ProposalRejected => "Proposal declined",
        Models.Enums.NotificationType.NewMessage => "New message",
        Models.Enums.NotificationType.ReviewReceived => "New review",
        Models.Enums.NotificationType.SupportReply => "Support reply",
        _ => "WebOfWork",
    };

    /// <summary>
    /// Where a tap should land.
    ///
    /// Prefers the related entity over the type, so a push and an in-app tap on
    /// the same notification go to the same place. Routing purely off the type sent
    /// every contract push to the notification list while the identical row tapped
    /// inside the app opened the contract -- the two paths disagreeing about the
    /// same row is exactly the kind of thing that reads as a broken app.
    ///
    /// A logical name rather than a URL, so a web and a mobile client can both
    /// interpret it.
    /// </summary>
    private static string RouteFor(Notification notification)
    {
        // "ChatRoom" is the value `PublishAsync` is actually called with for a
        // message; `RelatedEntityType` is a free-form string, so compare loosely.
        if (notification.RelatedEntityType?.Equals("ChatRoom", StringComparison.OrdinalIgnoreCase) == true)
            return "chat";

        if (notification.RelatedEntityId is > 0)
        {
            return notification.RelatedEntityType?.ToLowerInvariant() switch
            {
                "contract" => "contract",
                "proposal" => "proposals",
                "invitation" => "invitations",
                "dispute" => "dispute",
                _ => "notifications",
            };
        }

        return "notifications";
    }

    private NotificationPreferenceResponse Project(
        NotificationPreference? preference,
        IReadOnlyCollection<NotificationMuteDto> muted,
        UserRole role)
    {
        // With no saved row, every channel reports its catalogue default -- which
        // is why SMS reads false for a user who has never opened settings, and
        // true for a freelancer who explicitly switched it on.
        bool On(NotificationChannel c) => preference is null
            ? NotificationCatalogue.DefaultFor(c)
            : preference.MasterEnabled && preference.EnabledFor(c);

        var channels = NotificationCatalogue.ChannelsFor(role)
            .Select(c => new NotificationChannelOptionDto(
                NotificationChannelNames.ToWire(c),
                ChannelLabel(c),
                ChannelDescription(c),
                On(c),
                NotificationCatalogue.DefaultFor(c)))
            .ToList();

        // Only the types that apply to this role, each carrying the subset of
        // channels it can actually use. A client never renders a switch the
        // dispatcher would ignore.
        var types = NotificationCatalogue.All
            .Where(d => NotificationCatalogue.AppliesToRole(d, role))
            .Select(d => new NotificationTypeOptionDto(
                d.Type,
                d.Label,
                d.Description,
                d.Channels
                    .Where(c => NotificationCatalogue.ChannelAvailableTo(role, c))
                    .Select(NotificationChannelNames.ToWire)
                    .ToList()))
            .ToList();

        return new NotificationPreferenceResponse
        {
            MasterEnabled = preference?.MasterEnabled ?? true,
            InAppEnabled = On(NotificationChannel.InApp),
            PushEnabled = On(NotificationChannel.Push),
            SmsEnabled = On(NotificationChannel.Sms),
            EmailEnabled = On(NotificationChannel.Email),
            Channels = channels,
            Types = types,
            Muted = muted.OrderBy(m => m.Type, StringComparer.Ordinal)
                         .ThenBy(m => m.Channel, StringComparer.Ordinal)
                         .ToList(),
            // Derived from Muted, so a client reading either field sees the same
            // answer.
            MutedTypes = muted.Select(m => m.Type).Distinct(StringComparer.Ordinal)
                              .OrderBy(t => t, StringComparer.Ordinal).ToList(),
            PushConfigured = _push.IsConfigured,
        };
    }

    private static string ChannelLabel(NotificationChannel c) => c switch
    {
        NotificationChannel.InApp => "In-app notifications",
        NotificationChannel.Email => "Email",
        NotificationChannel.Sms => "SMS",
        NotificationChannel.Push => "Push notifications",
        _ => "Notifications",
    };

    private static string ChannelDescription(NotificationChannel c) => c switch
    {
        NotificationChannel.InApp => "Shown in the notifications tray and as a live update.",
        NotificationChannel.Email => "A short email for important events. Not sent for chat messages.",
        // Stated plainly, because SMS costs real money per message and the gateway
        // is not something to be opted into by accident.
        NotificationChannel.Sms => "Text messages. Costs money per message, and stays off unless you turn it on.",
        NotificationChannel.Push => "Sent to your registered devices, including when the app is closed.",
        _ => "",
    };
}
