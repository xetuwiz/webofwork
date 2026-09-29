using WebOfWork.Api.Models.Enums;

namespace WebOfWork.Api.Models;

/// <summary>
/// Single source of truth for the wire format of notification_type.
/// The reference sends snake_case ("proposal_accepted", "invitation_received", ...).
/// <see cref="Enum.ToString()"/> produces PascalCase and
/// <c>ToLowerInvariant()</c> would collapse it to "proposalaccepted" (no underscore),
/// which the frontend's getStyleMap lookup does not match. Always use this helper.
/// </summary>
public static class NotificationWireFormat
{
    public static string ToWireFormat(NotificationType type) => type switch
    {
        NotificationType.WorkflowApprovalRequired => "workflow_approval_required",
        NotificationType.ProposalAccepted => "proposal_accepted",
        NotificationType.InvitationReceived => "invitation_received",
        NotificationType.OfferReceived => "offer_received",
        NotificationType.ProposalRejected => "proposal_rejected",
        NotificationType.ContractStarted => "contract_started",
        NotificationType.WorkDelivered => "delivered",
        NotificationType.RevisionRequested => "revision_requested",
        NotificationType.RevisionAccepted => "revision_accepted",
        NotificationType.RevisionRejected => "revision_rejected",
        NotificationType.ContractCompleted => "contract_completed",
        NotificationType.PaymentCredited => "payment_credited",
        NotificationType.NewMessage => "new_message",
        NotificationType.PayoutReleased => "payout_released",
        NotificationType.DisputeOpened => "dispute_opened",
        NotificationType.DisputeResolved => "dispute_resolved",
        NotificationType.ReviewReceived => "review_received",
        NotificationType.SupportReply => "support_reply",
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unmapped NotificationType.")
    };

    /// <summary>Converts an already-stringified type (e.g. from a DTO) back to the wire format.</summary>
    public static string ToWireFormat(string typeName)
    {
        // NotificationService.Project already stores the *wire* value (e.g.
        // "delivered"), so this is routinely called with an output of the other
        // overload. Round-tripping it through Enum.TryParse threw, which surfaced
        // as a 400 and took down the whole notifications list.
        if (Enum.TryParse<NotificationType>(typeName, ignoreCase: true, out var parsed))
            return ToWireFormat(parsed);
        if (WireValues.Contains(typeName))
            return typeName;
        throw new ArgumentOutOfRangeException(nameof(typeName), typeName, "Unmapped notification type name.");
    }

    /// <summary>Every value <see cref="ToWireFormat(NotificationType)"/> can emit.</summary>
    private static readonly HashSet<string> WireValues = new(StringComparer.Ordinal)
    {
        "workflow_approval_required", "proposal_accepted", "invitation_received",
        "offer_received", "proposal_rejected", "contract_started", "delivered",
        "revision_requested", "revision_accepted", "revision_rejected",
        "contract_completed", "payment_credited", "new_message", "payout_released",
        "dispute_opened", "dispute_resolved", "review_received", "support_reply",
    };

    /// <summary>
    /// Read-only view of the wire vocabulary, so the notification-preference
    /// endpoints can validate an incoming mute list and render the full set of
    /// toggles to a client. Kept in one place so adding a
    /// <see cref="NotificationType"/> is a one-line change here and the settings
    /// screen picks it up.
    /// </summary>
    public static IReadOnlyCollection<string> AllWireValues => WireValues;

    /// <summary>True when <paramref name="value"/> is a known wire type name.</summary>
    public static bool IsKnownWireValue(string value) => WireValues.Contains(value);
}
