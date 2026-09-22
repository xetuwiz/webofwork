using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using WebOfWork.Api.Hubs;
using WebOfWork.Api.Models.Enums;
using WebOfWork.Api.Services;

namespace WebOfWork.Api.Jobs;

public class PublishNotificationJob
{
    private readonly INotificationPublisher _publisher;
    private readonly IHubContext<NotificationHub> _hubContext;
    private readonly ILogger<PublishNotificationJob> _logger;

    public PublishNotificationJob(
        INotificationPublisher publisher,
        IHubContext<NotificationHub> hubContext,
        ILogger<PublishNotificationJob> logger)
    {
        _publisher = publisher;
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task ExecuteAsync(Guid recipientId, string message, NotificationType type, CancellationToken ct = default)
    {
        var notification = await _publisher.PublishAsync(recipientId, type, message, ct: ct);
        await _hubContext.Clients.Group($"user_{recipientId:N}").SendAsync("NotificationReceived", notification, ct);
        _logger.LogInformation("Published queued notification {NotificationId} to user {RecipientId}", notification.Id, recipientId);
    }
}
