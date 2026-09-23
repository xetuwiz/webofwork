using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using WebOfWork.Api.Data;
using WebOfWork.Api.Hubs;
using WebOfWork.Api.Integrations.Email;
using WebOfWork.Api.Models.Enums;
using WebOfWork.Api.Services;

namespace WebOfWork.Api.Jobs;

public sealed class ContractOfferNotificationJob
{
    private readonly WebOfWorkDbContext _db;
    private readonly INotificationPublisher _publisher;
    private readonly IEmailSender _emailSender;
    private readonly IHubContext<NotificationHub> _hubContext;

    public ContractOfferNotificationJob(
        WebOfWorkDbContext db,
        INotificationPublisher publisher,
        IEmailSender emailSender,
        IHubContext<NotificationHub> hubContext)
    {
        _db = db;
        _publisher = publisher;
        _emailSender = emailSender;
        _hubContext = hubContext;
    }

    public async Task ExecuteAsync(Guid recipientId, int contractId, decimal amount, CancellationToken ct = default)
    {
        var recipient = await _db.Users.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == recipientId, ct)
            ?? throw new KeyNotFoundException($"Freelancer user {recipientId} was not found.");

        var message = $"You have received a new contract offer for {amount:0.00} USD on WebOfWork!";
        var notification = await _publisher.PublishAsync(
            recipientId,
            NotificationType.OfferReceived,
            message,
            nameof(Models.Contract),
            contractId,
            ct,
            amount);

        await _hubContext.Clients.Group($"user_{recipientId:N}")
            .SendAsync("NotificationReceived", notification, ct);

        await _emailSender.SendAsync(
            recipient.Email,
            "New WebOfWork contract offer",
            $"<p>{message}</p><p>Contract #{contractId}</p>",
            ct);
    }
}
