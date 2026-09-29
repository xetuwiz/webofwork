using System;
using System.ComponentModel.DataAnnotations;
using WebOfWork.Api.Common.Interfaces;

namespace WebOfWork.Api.Models;

/// <summary>
/// Per-user channel switches for the notification domain.
///
/// One row per user, created lazily on first read or write. Absence means "all
/// channels on", so a user who has never opened the settings screen keeps
/// receiving everything -- the migration therefore does not backfill rows for
/// existing users, which would otherwise have pinned them to whatever the
/// default happened to be at migration time.
///
/// The three channels map exactly to the three dispatchers
/// <see cref="Services.NotificationService"/> can reach:
///   * <c>InApp</c>  -- the notification row plus the WebSocket push
///   * <c>Push</c>   -- FCM, for when the app process is not running
///   * <c>Sms</c>    -- the existing SMS gateway
/// Email is deliberately absent: <c>IEmailSender</c> exists but is wired only to
/// OTP and password-reset jobs, so an "email notifications" switch would be a
/// control that silently does nothing.
/// </summary>
public class NotificationPreference : IAuditableEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UserId { get; set; }

    /// Non-nullable to match the rest of the model layer: the FK is required, and
    /// `ApplySoftDeletedUserFilters` dereferences this navigation, so a nullable
    /// annotation would turn each of those filters into a nullability warning.
    public User User { get; set; } = null!;

    public bool InAppEnabled { get; set; } = true;
    public bool PushEnabled { get; set; } = true;

    /// <summary>Off by default: see <see cref="NotificationCatalogue.DefaultFor"/>.</summary>
    public bool SmsEnabled { get; set; }

    /// <summary>Business-notification email, via the existing <c>IEmailSender</c>.</summary>
    public bool EmailEnabled { get; set; } = true;

    /// <summary>
    /// Master switch. Off silences every channel for every type, which is what a
    /// user reaches for when they want the app quiet -- far more often than
    /// walking 17 individual toggles.
    /// </summary>
    public bool MasterEnabled { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Reads a channel switch by name.</summary>
    public bool EnabledFor(NotificationChannel channel) => channel switch
    {
        NotificationChannel.InApp => InAppEnabled,
        NotificationChannel.Email => EmailEnabled,
        NotificationChannel.Sms => SmsEnabled,
        NotificationChannel.Push => PushEnabled,
        _ => false,
    };

    /// <summary>Writes a channel switch by name.</summary>
    public void SetEnabled(NotificationChannel channel, bool value)
    {
        switch (channel)
        {
            case NotificationChannel.InApp: InAppEnabled = value; break;
            case NotificationChannel.Email: EmailEnabled = value; break;
            case NotificationChannel.Sms: SmsEnabled = value; break;
            case NotificationChannel.Push: PushEnabled = value; break;
        }
    }
}

/// <summary>
/// A single per-type mute, e.g. "do not tell me about contract completions".
///
/// Stored as the *absence* of a row rather than a row with a flag, so the common
/// case (everything on) costs nothing and "muted" is an explicit act the user can
/// see in the settings screen.
/// </summary>
public class NotificationTypePreference
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UserId { get; set; }

    /// Non-nullable: the FK is required, and the soft-delete filter registered in
    /// `ApplySoftDeletedUserFilters` dereferences this navigation.
    public User User { get; set; } = null!;

    /// The wire name (<c>NotificationWireFormat</c>), not the enum name, so a
    /// client that reads a stored mute back does not have to know the C# casing.
    /// The value is validated against the known set in
    /// <c>NotificationPreferenceService</c>.
    [MaxLength(64)]
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// The channel this mute applies to, as the lower-cased
    /// <see cref="NotificationChannel"/> member name.
    ///
    /// This used not to exist: one row muted a type on every channel at once, so
    /// "don't text me about messages" and "don't show me messages" were the same
    /// setting and no per-channel choice was expressible. The column is what
    /// makes the settings grid mean anything.
    ///
    /// Defaults to <c>in_app</c>, which is also how an empty value is read. Rows
    /// written before this column existed muted the type everywhere; defaulting
    /// them to in-app keeps that meaning instead of silently narrowing it.
    /// </summary>
    [MaxLength(16)]
    public string Channel { get; set; } = NotificationChannelNames.InApp;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>Wire names for <see cref="NotificationChannel"/>, lower-cased to match the type vocabulary.</summary>
public static class NotificationChannelNames
{
    public const string InApp = "in_app";
    public const string Email = "email";
    public const string Sms = "sms";
    public const string Push = "push";

    public static string ToWire(NotificationChannel channel) => channel switch
    {
        NotificationChannel.InApp => InApp,
        NotificationChannel.Email => Email,
        NotificationChannel.Sms => Sms,
        NotificationChannel.Push => Push,
        _ => InApp,
    };

    /// <summary>Unrecognised or empty input resolves to in-app rather than throwing.</summary>
    public static NotificationChannel Parse(string? wire) => wire?.Trim().ToLowerInvariant() switch
    {
        Email => NotificationChannel.Email,
        Sms => NotificationChannel.Sms,
        Push => NotificationChannel.Push,
        _ => NotificationChannel.InApp,
    };

    public static bool IsKnown(string? wire) => wire?.Trim().ToLowerInvariant() is Email or Sms or Push or InApp;
}
