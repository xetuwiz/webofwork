using System;
using System.Collections.Generic;
using System.Linq;

namespace WebOfWork.Api.DTOs.Notifications;

/// <summary>
/// One (type, channel) pair the user has switched off.
/// </summary>
/// <remarks>
/// The wire shape is what the settings screen sends and receives. It is a pair
/// rather than a flat type list because a mute is no longer global: muting
/// <c>new_message</c> for <c>email</c> must not also hide it in the tray.
/// </remarks>
public record NotificationMuteDto(string Type, string Channel);

/// <summary>One type, as the settings screen renders it.</summary>
public record NotificationTypeOptionDto(
    string Type,
    string Label,
    string Description,
    List<string> Channels);

/// <summary>One channel, as the settings screen renders it.</summary>
/// <param name="Key">"in_app" | "email" | "sms" | "push".</param>
/// <param name="Enabled">Current state. Absent for a channel with no row yet.</param>
public record NotificationChannelOptionDto(
    string Key,
    string Label,
    string Description,
    bool Enabled,
    bool DefaultEnabled);

/// <summary>Wire shape of a user's channel switches, plus their per-type mutes.</summary>
public class NotificationPreferenceResponse
{
    /// <summary>
    /// Master switch. Off silences everything; the per-channel and per-type
    /// controls are preserved underneath it rather than reset, so turning
    /// notifications back on restores what the user had chosen.
    /// </summary>
    public bool MasterEnabled { get; set; } = true;

    public bool InAppEnabled { get; set; } = true;
    public bool PushEnabled { get; set; } = true;

    /// <summary>Off by default. See <c>NotificationCatalogue.DefaultFor</c>.</summary>
    public bool SmsEnabled { get; set; }

    public bool EmailEnabled { get; set; } = true;

    /// <summary>Channels this role may configure at all.</summary>
    public List<NotificationChannelOptionDto> Channels { get; set; } = new();

    /// <summary>Types that apply to this role, each with the channels it can use.</summary>
    public List<NotificationTypeOptionDto> Types { get; set; } = new();

    /// <summary>The muted (type, channel) pairs.</summary>
    public List<NotificationMuteDto> Muted { get; set; } = new();

    /// <summary>
    /// Flat list of muted type names, across all channels.
    /// </summary>
    /// <remarks>
    /// Retained so the older clients, which send and read a flat
    /// <c>muted_types</c>, keep working. It is derived from <see cref="Muted"/>,
    /// so the two cannot disagree: muting a type on any channel lists it here.
    /// A client that only understands this sees "muted everywhere", which is the
    /// safe direction -- over-suppressing beats re-notifying someone who asked
    /// not to be.
    /// </remarks>
    public List<string> MutedTypes { get; set; } = new();

    /// <summary>
    /// Whether the server actually has working FCM credentials.
    ///
    /// Exposed because the failure mode is otherwise invisible: with the config
    /// key present but the service-account file missing, `FirebasePushSender`
    /// disables itself, every publish silently skips the push, and the only
    /// evidence is one line in the server log that nobody reads. A client can
    /// surface this as "push is not configured" instead of accepting a switch that
    /// does nothing.
    /// </summary>
    public bool PushConfigured { get; set; }
}

/// <summary>Partial update. Null means "leave alone", so a client can flip one switch.</summary>
/// <param name="Muted">
/// Replaces the whole mute set. Null leaves it untouched; an empty list clears
/// every mute. Replace-and-not-merge, because a settings screen sends the state
/// it just rendered and a merge would never let a user *un*-mute anything.
/// </param>
/// <param name="MutedTypes">
/// The older flat form. When present it is treated as "mute on every channel",
/// which is exactly what it meant before the channel column existed.
/// </param>
public record UpdateNotificationPreferenceRequest(
    bool? MasterEnabled = null,
    bool? InAppEnabled = null,
    bool? PushEnabled = null,
    bool? SmsEnabled = null,
    bool? EmailEnabled = null,
    List<NotificationMuteDto>? Muted = null,
    List<string>? MutedTypes = null);

/// <summary>Registers (or re-registers) one device for push.</summary>
/// <param name="Token">The FCM registration token. Required and non-empty.</param>
/// <param name="Platform">"android" today.</param>
/// <param name="DeviceName">Optional label for the settings list.</param>
public record RegisterDeviceRequest(
    string Token,
    string? Platform = null,
    string? DeviceName = null);

/// <summary>One registered device, as shown in the settings screen.</summary>
public record DeviceResponse(
    string Token,
    string Platform,
    string? DeviceName,
    DateTime LastSeenAt);

/// <summary>The title/body/data a push carried, for the client's tray handler.</summary>
/// <param name="Title">Shade title.</param>
/// <param name="Body">Shade body.</param>
/// <param name="Type">Wire-format notification type, e.g. "contract_completed".</param>
/// <param name="RelatedEntityType">"Contract", "Proposal", ... or null.</param>
/// <param name="RelatedEntityId">Id of the related row, or null.</param>
public record PushPayload(
    string? Title,
    string? Body,
    string Type,
    string? RelatedEntityType,
    int? RelatedEntityId,
    string? Route);
