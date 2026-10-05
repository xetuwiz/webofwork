using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WebOfWork.Api.Data;
using WebOfWork.Api.DTOs.Notifications;
using WebOfWork.Api.Integrations.Email;
using WebOfWork.Api.Integrations.Sms;
using WebOfWork.Api.Integrations.Sms.Strategies;
using WebOfWork.Api.Models;
using WebOfWork.Api.Models.Enums;

using WebOfWork.Api.Middleware;

namespace WebOfWork.Api.Services;

public interface INotificationPublisher
{
    Task<Notification> PublishAsync(
        Guid recipientId,
        NotificationType type,
        string message,
        string? relatedEntityType = null,
        int? relatedEntityId = null,
        CancellationToken ct = default,
        params object[] smsArgs);

    Task TriggerFromDomainEventAsync<TEvent>(TEvent domainEvent, CancellationToken ct = default) where TEvent : notnull;
}

public interface INotificationService
{
    Task<NotificationListResponse> GetNotificationsAsync(Guid userId, bool? isRead = null, int limit = 50, CancellationToken ct = default, UserRole? targetRole = null);
    Task<NotificationResponse> MarkAsReadAsync(Guid userId, int notificationId, CancellationToken ct = default);
    Task<int> MarkAllAsReadAsync(Guid userId, CancellationToken ct = default);
    Task DeleteNotificationAsync(Guid userId, int notificationId, CancellationToken ct = default);
}

public class NotificationService : INotificationService, INotificationPublisher
{
    // The SMS allow-list that used to live here is gone. It had drifted from the
    // strategies actually registered in SmsStrategyFactory: it listed
    // `NewMessage`, for which no strategy exists, and omitted nothing it should
    // have had. `NotificationCatalogue` is now the single place that says which
    // types can travel over which channel, and the settings screen renders from
    // the same list -- so a toggle and the dispatcher cannot disagree.

    private readonly WebOfWorkDbContext _db;
    private readonly ISmsSender _smsSender;
    private readonly SmsStrategyFactory _smsStrategyFactory;
    private readonly IEmailSender _emailSender;
    private readonly ILogger<NotificationService> _logger;
    private readonly WebSocketGroupRegistry _sockets;
    private readonly INotificationPreferenceService _preferences;

    public NotificationService(
        WebOfWorkDbContext db,
        ISmsSender smsSender,
        SmsStrategyFactory smsStrategyFactory,
        IEmailSender emailSender,
        ILogger<NotificationService> logger,
        WebSocketGroupRegistry sockets,
        INotificationPreferenceService preferences)
    {
        _db = db;
        _smsSender = smsSender;
        _smsStrategyFactory = smsStrategyFactory;
        _emailSender = emailSender;
        _logger = logger;
        _sockets = sockets;
        _preferences = preferences;
    }

    /// <summary>
    /// The role to filter the catalogue by, when a user row's role is not a value
    /// the enum actually defines.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>User</c> carries <em>two</em> role columns: <c>Role</c> and
    /// <c>CurrentRole</c>. <c>UserRole</c> is declared <c>Client = 1</c>,
    /// <c>Freelancer = 2</c>, <c>Admin = 3</c> -- there is deliberately no zero
    /// member, so a row that never set either column reads back as
    /// <c>(UserRole)0</c>.
    /// </para>
    /// <para>
    /// That matters because the catalogue gates delivery on the role, and
    /// <c>AppliesToRole</c> asks whether the role is in the descriptor's list.
    /// Role <c>0</c> is in no list, so every channel resolves false and the
    /// publish returns early -- logging "every channel disabled", which is
    /// indistinguishable from a user who muted everything. Any user row whose
    /// <c>CurrentRole</c> was never written would silently stop receiving
    /// notifications, with the log actively pointing at the wrong cause.
    /// </para>
    /// <para>
    /// So: prefer <c>CurrentRole</c>, fall back to <c>Role</c>, and if neither is
    /// defined fall back to <c>Client</c>. <c>Client</c> is the right last resort
    /// because it is the least privileged -- it has no push and no SMS, so a
    /// corrupt role degrades to in-app-only rather than granting channels the
    /// user may not be entitled to.
    /// </para>
    /// <para>
    /// This is also not a delivery-safety question. The publish is targeted: the
    /// caller already decided this specific person should receive this specific
    /// event, so falling back cannot leak anything. It only decides which
    /// <em>channels</em> are used.
    /// </para>
    /// </remarks>
    private static UserRole ResolvePublishRole(User recipient, ILogger logger)
    {
        if (Enum.IsDefined(recipient.CurrentRole)) return recipient.CurrentRole;

        if (Enum.IsDefined(recipient.Role))
        {
            logger.LogWarning(
                "User {UserId} has no valid CurrentRole; falling back to Role={Role} for notification delivery",
                recipient.Id, recipient.Role);
            return recipient.Role;
        }

        logger.LogError(
            "User {UserId} has neither CurrentRole nor Role set to a valid UserRole; " +
            "treating as Client, so only in-app notifications will be delivered",
            recipient.Id);
        return UserRole.Client;
    }

    /// <summary>
    /// Subject line for the email channel. Shares the catalogue's vocabulary
    /// rather than inventing a second set of names.
    /// </summary>
    private static string EmailTitleFor(NotificationType type) =>
        NotificationCatalogue.Find(NotificationWireFormat.ToWireFormat(type))?.Label
        ?? "WebOfWork notification";

    /// <param name="targetRole">
    /// When supplied, a notification is visible only if it carries no TargetRole or
    /// its TargetRole matches. This used to be applied by the duplicate
    /// `/api/communication/notifications` route rather than here, so the canonical
    /// `/api/notifications` list showed a user notifications aimed at the other role.
    /// Applied to the counts as well as the list, otherwise the badge and the list
    /// disagree.
    /// </param>
    public async Task<NotificationListResponse> GetNotificationsAsync(Guid userId, bool? isRead = null, int limit = 50, CancellationToken ct = default, UserRole? targetRole = null)
    {
        if (limit <= 0) limit = 50;
        if (limit > 200) limit = 200;

        // Stored values are lower-case ("freelancer", "client"), while the enum member
        // is PascalCase, so the comparison has to be case-insensitive. Comparing
        // `n.TargetRole == targetRole.ToString()` silently matched nothing and hid
        // every notification from the user.
        var roleName = targetRole?.ToString().ToLowerInvariant();

        var query = _db.Notifications.AsNoTracking().Where(n => n.RecipientId == userId);
        if (isRead.HasValue) query = query.Where(n => n.IsRead == isRead.Value);
        if (roleName != null)
        {
            query = query.Where(n => n.TargetRole == null || n.TargetRole == "" || n.TargetRole.ToLower() == roleName);
        }

        var rows = await query.OrderByDescending(n => n.CreatedAt).Take(limit).ToListAsync(ct);

        var items = rows.Select(Project).ToList();

        var countQuery = _db.Notifications.AsNoTracking().Where(n => n.RecipientId == userId);
        if (roleName != null)
        {
            countQuery = countQuery.Where(n => n.TargetRole == null || n.TargetRole == "" || n.TargetRole.ToLower() == roleName);
        }

        var totalCount = await countQuery.CountAsync(ct);
        var unreadCount = await countQuery.CountAsync(n => !n.IsRead, ct);

        return new NotificationListResponse
        {
            UnreadCount = unreadCount,
            TotalCount = totalCount,
            Items = items
        };
    }

    public async Task<NotificationResponse> MarkAsReadAsync(Guid userId, int notificationId, CancellationToken ct = default)
    {
        var n = await FindOwnOrThrow(userId, notificationId, ct);
        n.IsRead = true;
        await _db.SaveChangesAsync(ct);
        return Project(n);
    }

    public async Task<int> MarkAllAsReadAsync(Guid userId, CancellationToken ct = default)
    {
        var toUpdate = await _db.Notifications.Where(n => n.RecipientId == userId && !n.IsRead).ToListAsync(ct);
        foreach (var n in toUpdate) n.IsRead = true;
        if (toUpdate.Count > 0) await _db.SaveChangesAsync(ct);
        return toUpdate.Count;
    }

    public async Task DeleteNotificationAsync(Guid userId, int notificationId, CancellationToken ct = default)
    {
        var n = await FindOwnOrThrow(userId, notificationId, ct);
        _db.Notifications.Remove(n);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<Notification> PublishAsync(
        Guid recipientId,
        NotificationType type,
        string message,
        string? relatedEntityType = null,
        int? relatedEntityId = null,
        CancellationToken ct = default,
        params object[] smsArgs)
    {
        var recipient = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == recipientId, ct);
        if (recipient == null)
        {
            _logger.LogWarning("Skipped notification publish for unknown recipient {RecipientId}", recipientId);
            throw new KeyNotFoundException($"Recipient {recipientId} not found");
        }

        // The role the catalogue filters on. See `ResolvePublishRole` for why this
        // is not simply `recipient.CurrentRole`.
        var role = ResolvePublishRole(recipient, _logger);

        var notification = new Notification
        {
            RecipientId = recipientId,
            Type = type,
            Message = message,
            RelatedEntityType = relatedEntityType,
            RelatedEntityId = relatedEntityId,
            TargetRole = WebOfWork.Api.Helpers.SnakeCase.Of(role)
        };

        // Resolved before the row is written, so a user who has disabled every
        // channel does not accumulate rows nothing will ever surface.
        //
        // The recipient's *role* is an input now, not just a label: the catalogue
        // decides which channels a role can use at all, and which types apply to
        // it. That is the same answer the settings screen renders from, so a
        // toggle and the dispatcher cannot disagree.
        var wireType = NotificationWireFormat.ToWireFormat(type);
        var channels = await _preferences.ResolveChannelsAsync(
            recipientId, wireType, role, ct);

        // `Any` over the resolution, not a hand-picked pair.
        //
        // The old guard was `if (!inApp && !push) return;` and never mentioned
        // SMS, so a user with in-app off, push off and SMS on got nothing at all
        // despite having SMS switched on -- and the log line claimed every
        // channel was disabled, which is exactly what made it hard to find.
        if (!channels.InApp && !channels.Push && !channels.Sms && !channels.Email)
        {
            _logger.LogInformation(
                "Skipped notification {Type} for {RecipientId}: every channel disabled.",
                wireType, recipientId);
            return notification;
        }

        if (channels.InApp)
        {
            _db.Notifications.Add(notification);
            await _db.SaveChangesAsync(ct);

            foreach (var socket in _sockets.GetUsers(recipientId))
            {
                // Push the **same** projection the list endpoint returns.
                //
                // This used to hand-roll an anonymous object with
                // `notification_type` and `related_id`, which is a different shape
                // from `NotificationResponse`'s `type` / `related_entity_id` /
                // `related_entity_type` -- and it dropped `related_entity_type`
                // entirely, so a client could not tell a contract notification from a
                // proposal one and had to keep a second parser for the push path.
                // The type field name also did not match `getStyleMap`-style lookups
                // built against the list response.
                try { await WebSocketBridgeMiddleware.Send(socket, Project(notification)); }
                catch (Exception ex) { _logger.LogDebug(ex, "Notification socket closed while publishing"); }
            }
        }

        if (channels.Push)
        {
            // Only meaningful once the row exists, because the push carries the
            // notification id for the client's "mark read on tap" call.
            if (notification.Id == 0)
            {
                _db.Notifications.Add(notification);
                await _db.SaveChangesAsync(ct);
            }
            await _preferences.DispatchPushAsync(notification, recipient.CurrentRole, ct);
        }

        if (channels.Email && !string.IsNullOrWhiteSpace(recipient.Email))
        {
            try
            {
                await _emailSender.SendAsync(
                    recipient.Email,
                    EmailTitleFor(type),
                    $"<p>{System.Net.WebUtility.HtmlEncode(notification.Message)}</p>",
                    ct);
            }
            catch (Exception ex)
            {
                // A mail server hiccup must not fail the publish, and must not
                // stop the remaining channels from firing.
                _logger.LogError(ex, "Failed to send notification email to {RecipientId}, type={Type}",
                    recipientId, type);
            }
        }

        // The catalogue decides whether SMS is even an option for this type, so
        // the old `SmsEnabledTypes` allow-list is gone. It had drifted: it listed
        // `NewMessage`, which has no strategy behind it, and omitted types the
        // strategies did cover. One list, consulted in one place.
        if (channels.Sms && !string.IsNullOrWhiteSpace(recipient.Phone))
        {
            try
            {
                var strategyKey = type.ToString();
                if (_smsStrategyFactory.TryGetValue(strategyKey, out var strategy))
                {
                    var smsText = strategy.GenerateMessage(smsArgs);
                    await _smsSender.SendSmsAsync(recipient.Phone, smsText);
                }
                else if (_smsStrategyFactory.TryGetValueFallback(type, out var fallback))
                {
                    var smsText = fallback.GenerateMessage(smsArgs.DefaultIfEmpty(message).ToArray());
                    await _smsSender.SendSmsAsync(recipient.Phone, smsText);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to dispatch SMS for notification {NotificationId}, type={Type}",
                    notification.Id, type);
            }
        }

        return notification;
    }

    public async Task TriggerFromDomainEventAsync<TEvent>(TEvent domainEvent, CancellationToken ct = default) where TEvent : notnull
    {
        var evtType = typeof(TEvent);
        _logger.LogDebug("TriggerFromDomainEvent {Event}", evtType.Name);

        switch (domainEvent)
        {
            case ContractStartedEvent e:
                await PublishAsync(e.RecipientId, NotificationType.ContractStarted,
                    $"Freelancer signed the contract for '{await ProjectTitleForContractAsync(e.ContractId, ct)}'. Work has started!",
                    relatedEntityType: nameof(Contract), relatedEntityId: e.ContractId, ct: ct).ConfigureAwait(false);
                break;
            case WorkDeliveredEvent e:
                await PublishAsync(e.RecipientId, NotificationType.WorkDelivered,
                    $"New work has been delivered for '{await ProjectTitleForContractAsync(e.ContractId, ct)}'",
                    relatedEntityType: nameof(Contract), relatedEntityId: e.ContractId, ct: ct).ConfigureAwait(false);
                break;
            case ContractCompletedEvent e:
                await PublishAsync(e.RecipientId, NotificationType.ContractCompleted,
                    $"The contract for '{await ProjectTitleForContractAsync(e.ContractId, ct)}' is officially complete!",
                    relatedEntityType: nameof(Contract), relatedEntityId: e.ContractId, ct: ct).ConfigureAwait(false);
                break;
            case PaymentCreditedEvent e:
                await PublishAsync(e.RecipientId, NotificationType.PaymentCredited,
                    $"Payment of ${e.Amount} has been credited to your account!",
                    relatedEntityType: nameof(Payment), relatedEntityId: e.PaymentId, ct: ct, e.Amount).ConfigureAwait(false);
                break;
            case PayoutReleasedEvent e:
                await PublishAsync(e.RecipientId, NotificationType.PayoutReleased,
                    $"A payout has been released to your account.",
                    relatedEntityType: nameof(Contract), relatedEntityId: e.ContractId, ct: ct, e.Amount).ConfigureAwait(false);
                break;
            case DisputeOpenedEvent e:
                await PublishAsync(e.RecipientId, NotificationType.DisputeOpened,
                    $"A dispute has been opened on your contract.",
                    relatedEntityType: nameof(Dispute), relatedEntityId: e.DisputeId, ct: ct).ConfigureAwait(false);
                break;
            case RevisionRequestedEvent e:
                await PublishAsync(e.RecipientId, NotificationType.RevisionRequested,
                    $"Revision requested for '{await ProjectTitleForContractAsync(e.RevisionId, ct)}'",
                    relatedEntityType: nameof(ContractRevision), relatedEntityId: e.RevisionId, ct: ct).ConfigureAwait(false);
                break;
            case OfferReceivedEvent e:
                await PublishAsync(e.RecipientId, NotificationType.OfferReceived,
                    $"You received a new contract offer for '{await ProjectTitleForInvitationAsync(e.InvitationId, ct)}' from {await ClientNameForInvitationAsync(e.InvitationId, ct)}",
                    relatedEntityType: nameof(Invitation), relatedEntityId: e.InvitationId, ct: ct).ConfigureAwait(false);
                break;
            case InvitationReceivedEvent e:
                await PublishAsync(e.RecipientId, NotificationType.InvitationReceived,
                    $"You received a new invitation for '{await ProjectTitleForInvitationAsync(e.InvitationId, ct)}'",
                    relatedEntityType: nameof(Invitation), relatedEntityId: e.InvitationId, ct: ct).ConfigureAwait(false);
                break;
            case ProposalAcceptedEvent e:
                await PublishAsync(e.RecipientId, NotificationType.ProposalAccepted,
                    $"Your proposal for '{await ProjectTitleForProposalAsync(e.ProposalId, ct)}' was accepted!",
                    relatedEntityType: nameof(Proposal), relatedEntityId: e.ProposalId, ct: ct).ConfigureAwait(false);
                break;
            default:
                _logger.LogWarning("TriggerFromDomainEvent: no handler for event type {EventType}", evtType.FullName);
                break;
        }
    }

    private async Task<string> ProjectTitleForContractAsync(int contractOrRevisionId, CancellationToken ct)
    {
        var contract = await _db.Contracts.AsNoTracking().Include(c => c.Project).FirstOrDefaultAsync(c => c.Id == contractOrRevisionId, ct);
        if (contract != null) return contract.Project.Title;
        var revision = await _db.ContractRevisions.AsNoTracking().Include(r => r.Contract).ThenInclude(c => c.Project).FirstOrDefaultAsync(r => r.Id == contractOrRevisionId, ct);
        return revision?.Contract.Project.Title ?? string.Empty;
    }


    private async Task<Notification> FindOwnOrThrow(Guid userId, int id, CancellationToken ct)
    {
        var n = await _db.Notifications.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new KeyNotFoundException($"Notification {id} not found.");
        if (n.RecipientId != userId)
            throw new UnauthorizedAccessException("This notification belongs to another user.");
        return n;
    }
    private async Task<string> ProjectTitleForInvitationAsync(int id, CancellationToken ct) => (await _db.Invitations.AsNoTracking().Include(i=>i.Project).FirstOrDefaultAsync(i=>i.Id==id,ct))?.Project.Title ?? string.Empty;
    private async Task<string> ClientNameForInvitationAsync(int id, CancellationToken ct) => (await _db.Invitations.AsNoTracking().Include(i=>i.Client).ThenInclude(c=>c.User).FirstOrDefaultAsync(i=>i.Id==id,ct))?.Client.User.FullName ?? string.Empty;
    private async Task<string> ProjectTitleForProposalAsync(int id, CancellationToken ct) => (await _db.Proposals.AsNoTracking().Include(p=>p.Project).FirstOrDefaultAsync(p=>p.Id==id,ct))?.Project.Title ?? string.Empty;



    private static NotificationResponse Project(Notification n) => new()
    {
        Id = n.Id,
        // The reference sends snake_case ("proposal_accepted"). ToString() would emit
        // PascalCase and break the client's getStyleMap lookup. Type is a string
        // property, so the global enum converter does not apply here — this
        // projection must render the wire format itself.
        Type = WebOfWork.Api.Models.NotificationWireFormat.ToWireFormat(n.Type),
        Message = n.Message,
        RelatedEntityType = n.RelatedEntityType,
        RelatedEntityId = n.RelatedEntityId,
        IsRead = n.IsRead,
        CreatedAt = n.CreatedAt,
        TargetRole = n.TargetRole
    };
}

public abstract record DomainNotificationEvent(Guid RecipientId);

public sealed record ContractStartedEvent(Guid RecipientId, int ContractId) : DomainNotificationEvent(RecipientId);
public sealed record WorkDeliveredEvent(Guid RecipientId, int ContractId) : DomainNotificationEvent(RecipientId);
public sealed record ContractCompletedEvent(Guid RecipientId, int ContractId) : DomainNotificationEvent(RecipientId);
public sealed record PaymentCreditedEvent(Guid RecipientId, int PaymentId, decimal Amount) : DomainNotificationEvent(RecipientId);
public sealed record PayoutReleasedEvent(Guid RecipientId, int ContractId, decimal Amount) : DomainNotificationEvent(RecipientId);
public sealed record DisputeOpenedEvent(Guid RecipientId, int DisputeId) : DomainNotificationEvent(RecipientId);
public sealed record RevisionRequestedEvent(Guid RecipientId, int RevisionId) : DomainNotificationEvent(RecipientId);
public sealed record OfferReceivedEvent(Guid RecipientId, int InvitationId) : DomainNotificationEvent(RecipientId);
public sealed record InvitationReceivedEvent(Guid RecipientId, int InvitationId) : DomainNotificationEvent(RecipientId);
public sealed record ProposalAcceptedEvent(Guid RecipientId, int ProposalId) : DomainNotificationEvent(RecipientId);
