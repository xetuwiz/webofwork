using System;
using System.ComponentModel.DataAnnotations;

namespace WebOfWork.Api.Models;

/// <summary>
/// One installable device that can receive FCM pushes.
///
/// Keyed on <see cref="Token"/> rather than a surrogate id: FCM rotates tokens on
/// a reinstall, a restore, and (periodically) on its own, and the client sends
/// whatever token it currently holds. Upserting on the token means a rotation
/// updates the existing row instead of accumulating dead ones, and re-registering
/// the same device from the same user is idempotent.
///
/// The composite (UserId, Token) is unique so one device cannot be claimed by two
/// accounts -- otherwise signing out on a shared tablet and signing in as someone
/// else leaves the previous user's notifications still routed to it.
/// </summary>
public class DeviceRegistration
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UserId { get; set; }

    /// Non-nullable: the FK is required, and the soft-delete filter registered in
    /// `ApplySoftDeletedUserFilters` dereferences this navigation.
    public User User { get; set; } = null!;

    /// The full FCM registration token. Text, not a fixed column: the value is
    /// ~150-200 chars today and has grown before.
    [MaxLength(512)]
    public string Token { get; set; } = string.Empty;

    /// "android" today. Present so an iOS/web client can be added without a
    /// migration, and so the settings screen can label the device list.
    [MaxLength(16)]
    public string Platform { get; set; } = "android";

    /// Free-form device label for the settings list ("Pixel 8"). Optional --
    /// `getDeviceInfo()` is not guaranteed to return a model name.
    [MaxLength(128)]
    public string? DeviceName { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// Last time this token was reported. Distinguishes "still in use" from
    /// "stale row we have not pruned yet" when deciding what to show the user.
    public DateTime LastSeenAt { get; set; } = DateTime.UtcNow;
}
