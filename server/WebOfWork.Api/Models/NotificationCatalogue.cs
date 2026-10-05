using System;
using System.Collections.Generic;
using System.Linq;
using WebOfWork.Api.Models.Enums;

namespace WebOfWork.Api.Models;

/// <summary>
/// The delivery channels a notification can travel over.
/// </summary>
/// <remarks>
/// Wire names are the enum member lower-cased, matching <c>NotificationWireFormat</c>,
/// so a client reads one vocabulary across types and channels.
/// </remarks>
public enum NotificationChannel
{
    /// <summary>The stored row plus the WebSocket push. Always available.</summary>
    InApp,

    /// <summary>Email, via the existing <c>IEmailSender</c>.</summary>
    Email,

    /// <summary>SMS, via the Android SMS gateway. Freelancer-only, off by default.</summary>
    Sms,

    /// <summary>FCM to a registered device. Freelancer-only, because only the
    /// freelancer build of the app can register one.</summary>
    Push,
}

/// <summary>
/// One notification type, and the channels + roles it is meaningful for.
/// </summary>
public sealed record NotificationTypeDescriptor(
    string Type,
    string Label,
    string Description,
    IReadOnlyList<NotificationChannel> Channels,
    IReadOnlyList<UserRole> Roles);

/// <summary>
/// The single source of truth for the notification matrix.
///
/// <para>
/// This exists because three separate places used to disagree about it: the SMS
/// allow-list in <c>NotificationService</c> (11 types, and it listed
/// <c>NewMessage</c>, which has no strategy), the push title switch in
/// <c>NotificationPreferenceService</c> (18 types), and the flat mute list on the
/// preference model, which could not express a per-channel choice at all.
/// A toggle rendered from one list and enforced by another is how a settings
/// screen ends up lying to the user.
/// </para>
///
/// <para>
/// <b>Channel availability.</b> SMS and push are freelancer-only, so the client can
/// hide those switches for clients and admins rather than showing a control that
/// could never fire. In-app and email are universal.
/// </para>
///
/// <para>
/// <b>Email curation.</b> Only consequential, low-frequency events get an email
/// channel. Nobody wants an email per chat message, and offering the toggle
/// invites exactly that.
/// </para>
/// </summary>
public static class NotificationCatalogue
{
    private static readonly NotificationChannel[] EveryChannel =
        { NotificationChannel.InApp, NotificationChannel.Email, NotificationChannel.Sms, NotificationChannel.Push };

    private static readonly NotificationChannel[] FreelancerOnly =
        { NotificationChannel.InApp, NotificationChannel.Sms, NotificationChannel.Push };

    private static readonly UserRole[] Both = { UserRole.Client, UserRole.Freelancer };
    private static readonly UserRole[] FreelancerOnlyRoles = { UserRole.Freelancer };
    private static readonly UserRole[] ClientOnly = { UserRole.Client };
    private static readonly UserRole[] Everyone = { UserRole.Client, UserRole.Freelancer, UserRole.Admin };

    /// <summary>
    /// Channel switch defaults, applied when a user has no saved row.
    /// </summary>
    /// <remarks>
    /// SMS defaults OFF. It costs real money per message, the gateway is not
    /// something a user should be opted into by accident, and it is the one
    /// channel that cannot currently be verified end to end. Every other channel
    /// defaults on so this change cannot silently mute anyone on upgrade.
    /// </remarks>
    public static bool DefaultFor(NotificationChannel channel) => channel switch
    {
        NotificationChannel.InApp => true,
        NotificationChannel.Email => true,
        NotificationChannel.Push => true,
        NotificationChannel.Sms => false,
        _ => true,
    };

    /// <summary>Whether a role can ever use a channel at all.</summary>
    public static bool ChannelAvailableTo(UserRole role, NotificationChannel channel) => channel switch
    {
        NotificationChannel.Sms or NotificationChannel.Push => role == UserRole.Freelancer,
        _ => true,
    };

    /// <summary>Every channel a given role may configure, in display order.</summary>
    public static IReadOnlyList<NotificationChannel> ChannelsFor(UserRole role) =>
        Enum.GetValues<NotificationChannel>().Where(c => ChannelAvailableTo(role, c)).ToList();

    private static NotificationTypeDescriptor D(
        NotificationType type, string label, string description,
        NotificationChannel[] channels, UserRole[] roles) =>
        // The wire name is derived, never written out. Hardcoding it here is how
        // "work_delivered" would end up in the settings screen while the server
        // emits "delivered" -- the enum -> wire mapping already exists and is the
        // only thing allowed to decide it.
        new(NotificationWireFormat.ToWireFormat(type), label, description, channels, roles);

    /// <summary>The full matrix, in the order the settings screen should show it.</summary>
    public static readonly IReadOnlyList<NotificationTypeDescriptor> All = new List<NotificationTypeDescriptor>
    {
        D(NotificationType.InvitationReceived, "Invitations received",
            "A client invites you to one of their projects.",
            EveryChannel, FreelancerOnlyRoles),
        D(NotificationType.OfferReceived, "Contract offers received",
            "A client sends you a contract offer to sign.",
            EveryChannel, FreelancerOnlyRoles),
        D(NotificationType.ProposalAccepted, "Proposals accepted",
            "A client accepts one of your proposals.",
            EveryChannel, FreelancerOnlyRoles),
        D(NotificationType.ProposalRejected, "Proposals declined",
            "A client declines one of your proposals.",
            EveryChannel, FreelancerOnlyRoles),
        D(NotificationType.ContractStarted, "Contracts started",
            "Both parties have signed and work has begun.",
            EveryChannel, Both),
        D(NotificationType.WorkDelivered, "Work delivered",
            "A freelancer submits a deliverable for your review.",
            EveryChannel, Both),
        D(NotificationType.RevisionRequested, "Changes requested",
            "The other side asks for changes to delivered work.",
            EveryChannel, Both),
        D(NotificationType.RevisionAccepted, "Changes accepted",
            "A requested change was accepted.",
            EveryChannel, Both),
        D(NotificationType.RevisionRejected, "Changes declined",
            "A requested change was declined.",
            EveryChannel, Both),
        D(NotificationType.ContractCompleted, "Contracts completed",
            "A contract is marked complete by both sides.",
            EveryChannel, Both),
        D(NotificationType.PaymentCredited, "Payments received",
            "Money is credited to your account.",
            new[] { NotificationChannel.InApp, NotificationChannel.Email, NotificationChannel.Sms },
            FreelancerOnlyRoles),
        D(NotificationType.PayoutReleased, "Payouts released",
            "A payout is released to your PayPal account.",
            new[] { NotificationChannel.InApp, NotificationChannel.Email, NotificationChannel.Sms },
            FreelancerOnlyRoles),
        D(NotificationType.DisputeOpened, "Disputes opened",
            "A dispute is raised on one of your contracts.",
            EveryChannel, Both),
        D(NotificationType.DisputeResolved, "Disputes resolved",
            "An admin rules on a dispute.",
            EveryChannel, Both),
        D(NotificationType.ReviewReceived, "Reviews received",
            "The other side leaves you a review.",
            new[] { NotificationChannel.InApp, NotificationChannel.Email },
            Both),
        D(NotificationType.NewMessage, "New messages",
            "Someone sends you a chat message.",
            // In-app only, as a product decision: an SMS per chat message is
            // expensive noise, and chat mostly arrives while the app is open.
            //
            // `NewMessageSmsStrategy` *does* exist in `SmsStrategyFactory`, so
            // this is not a capability gap -- the capability is deliberately
            // unused. An earlier version of this comment claimed there was "no
            // strategy behind it", which was false; the real problem with the old
            // allow-list was that it was a second, hand-maintained list that had
            // already drifted from this one.
            new[] { NotificationChannel.InApp, NotificationChannel.Push },
            Both),
        D(NotificationType.SupportReply, "Support replies",
            "Our team replies to a support ticket.",
            new[] { NotificationChannel.InApp, NotificationChannel.Email },
            Everyone),
        D(NotificationType.WorkflowApprovalRequired, "Actions needing your approval",
            "Something is waiting on an administrator to approve it.",
            new[] { NotificationChannel.InApp, NotificationChannel.Email },
            Everyone),
    };

    private static readonly Dictionary<string, NotificationTypeDescriptor> ByType =
        All.ToDictionary(d => d.Type, StringComparer.Ordinal);

    public static NotificationTypeDescriptor? Find(string? wireType) =>
        wireType != null && ByType.TryGetValue(wireType, out var d) ? d : null;

    public static bool IsKnownWireValue(string? wireType) => Find(wireType) != null;

    public static IEnumerable<string> AllWireValues => All.Select(d => d.Type);

    /// <summary>
    /// Whether a type may be delivered over a channel at all, ignoring the user's
    /// own preferences. This is the gate that stops a "chat over email" request.
    /// </summary>
    public static bool Supports(NotificationTypeDescriptor descriptor, NotificationChannel channel) =>
        descriptor.Channels.Contains(channel);

    /// <summary>
    /// Whether a type applies to a role. The publish path does not consult this --
    /// the 16 call sites already address the right person -- but the settings
    /// screen does, so a client is not shown a "Payouts released" toggle for an
    /// event that can never reach them.
    /// </summary>
    public static bool AppliesToRole(NotificationTypeDescriptor descriptor, UserRole role) =>
        descriptor.Roles.Contains(role);

    /// <summary>
    /// Email-only types. Kept as a named set because the dispatcher needs it and
    /// "descriptor.Channels contains Email" would also match in-app-only types if
    /// the array were ever reordered.
    /// </summary>
    public static bool EmailAllowed(string wireType) =>
        Find(wireType) is { } d && Supports(d, NotificationChannel.Email);
}
