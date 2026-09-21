using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WebOfWork.Api.Data;
using WebOfWork.Api.DTOs.Notifications;
using WebOfWork.Api.Integrations.Sms;
using WebOfWork.Api.Integrations.Sms.Strategies;
using WebOfWork.Api.Models;
using WebOfWork.Api.Models.Enums;

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
    Task<NotificationListResponse> GetNotificationsAsync(Guid userId, bool? isRead = null, int limit = 50, CancellationToken ct = default);
    Task<NotificationResponse> MarkAsReadAsync(Guid userId, int notificationId, CancellationToken ct = default);
    Task<int> MarkAllAsReadAsync(Guid userId, CancellationToken ct = default);
    Task DeleteNotificationAsync(Guid userId, int notificationId, CancellationToken ct = default);
}

public class NotificationService : INotificationService, INotificationPublisher
{
    private static readonly HashSet<NotificationType> SmsEnabledTypes = new()
    {
        NotificationType.ContractStarted,
        NotificationType.WorkDelivered,
        NotificationType.ContractCompleted,
        NotificationType.PaymentCredited,
        NotificationType.NewMessage,
        NotificationType.PayoutReleased,
        NotificationType.DisputeOpened,
        NotificationType.RevisionRequested,
        NotificationType.OfferReceived,
        NotificationType.InvitationReceived,
        NotificationType.ProposalAccepted
    };

    private readonly WebOfWorkDbContext _db;
    private readonly ISmsSender _smsSender;
    private readonly SmsStrategyFactory _smsStrategyFactory;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(
        WebOfWorkDbContext db,
        ISmsSender smsSender,
        SmsStrategyFactory smsStrategyFactory,
        ILogger<NotificationService> logger)
    {
        _db = db;
        _smsSender = smsSender;
        _smsStrategyFactory = smsStrategyFactory;
        _logger = logger;
    }

    public async Task<NotificationListResponse> GetNotificationsAsync(Guid userId, bool? isRead = null, int limit = 50, CancellationToken ct = default)
    {
        if (limit <= 0) limit = 50;
        if (limit > 200) limit = 200;

        var query = _db.Notifications.AsNoTracking().Where(n => n.RecipientId == userId);
        if (isRead.HasValue) query = query.Where(n => n.IsRead == isRead.Value);

        var rows = await query
            .OrderByDescending(n => n.CreatedAt)
            .Take(limit)
            .ToListAsync(ct);

        var items = rows.Select(Project).ToList();

        var totalCount = await _db.Notifications.CountAsync(n => n.RecipientId == userId, ct);
        var unreadCount = await _db.Notifications.CountAsync(n => n.RecipientId == userId && !n.IsRead, ct);

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
        var toUpdate = await _db.Notifications
            .Where(n => n.RecipientId == userId && !n.IsRead)
            .ToListAsync(ct);
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
        var recipient = await _db.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == recipientId, ct);
        if (recipient == null)
        {
            _logger.LogWarning("Skipped notification publish for unknown recipient {RecipientId}", recipientId);
            throw new KeyNotFoundException($"Recipient {recipientId} not found");
        }

        var notification = new Notification
        {
            RecipientId = recipientId,
            Type = type,
            Message = message,
            RelatedEntityType = relatedEntityType,
            RelatedEntityId = relatedEntityId
        };
        _db.Notifications.Add(notification);
        await _db.SaveChangesAsync(ct);

        if (SmsEnabledTypes.Contains(type) && !string.IsNullOrWhiteSpace(recipient.Phone))
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
                    $"Great news! Your contract has started on WebOfWork.",
                    relatedEntityType: nameof(Contract), relatedEntityId: e.ContractId, ct: ct).ConfigureAwait(false);
                break;
            case WorkDeliveredEvent e:
                await PublishAsync(e.RecipientId, NotificationType.WorkDelivered,
                    $"Work has been delivered on your contract. Please review.",
                    relatedEntityType: nameof(Contract), relatedEntityId: e.ContractId, ct: ct).ConfigureAwait(false);
                break;
            case ContractCompletedEvent e:
                await PublishAsync(e.RecipientId, NotificationType.ContractCompleted,
                    $"Your WebOfWork contract has been completed successfully.",
                    relatedEntityType: nameof(Contract), relatedEntityId: e.ContractId, ct: ct).ConfigureAwait(false);
                break;
            case PaymentCreditedEvent e:
                await PublishAsync(e.RecipientId, NotificationType.PaymentCredited,
                    $"A payment has been credited to your WebOfWork account.",
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
                    $"A revision has been requested on your delivery.",
                    relatedEntityType: nameof(ContractRevision), relatedEntityId: e.RevisionId, ct: ct).ConfigureAwait(false);
                break;
            case OfferReceivedEvent e:
                await PublishAsync(e.RecipientId, NotificationType.OfferReceived,
                    $"You have received a new offer on WebOfWork.",
                    relatedEntityType: nameof(Invitation), relatedEntityId: e.InvitationId, ct: ct).ConfigureAwait(false);
                break;
            case InvitationReceivedEvent e:
                await PublishAsync(e.RecipientId, NotificationType.InvitationReceived,
                    $"You have received a new project invitation on WebOfWork.",
                    relatedEntityType: nameof(Invitation), relatedEntityId: e.InvitationId, ct: ct).ConfigureAwait(false);
                break;
            case ProposalAcceptedEvent e:
                await PublishAsync(e.RecipientId, NotificationType.ProposalAccepted,
                    $"Congratulations! Your proposal has been accepted on WebOfWork.",
                    relatedEntityType: nameof(Proposal), relatedEntityId: e.ProposalId, ct: ct).ConfigureAwait(false);
                break;
            default:
                _logger.LogWarning("TriggerFromDomainEvent: no handler for event type {EventType}", evtType.FullName);
                break;
        }
    }

    private async Task<Notification> FindOwnOrThrow(Guid userId, int id, CancellationToken ct)
    {
        var n = await _db.Notifications
            .FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new KeyNotFoundException($"Notification {id} not found.");
        if (n.RecipientId != userId)
            throw new UnauthorizedAccessException("This notification belongs to another user.");
        return n;
    }

    private static NotificationResponse Project(Notification n) => new()
    {
        Id = n.Id,
        Type = n.Type.ToString(),
        Message = n.Message,
        RelatedEntityType = n.RelatedEntityType,
        RelatedEntityId = n.RelatedEntityId,
        IsRead = n.IsRead,
        CreatedAt = n.CreatedAt
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
