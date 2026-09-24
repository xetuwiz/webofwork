using System.Collections.Generic;
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WebOfWork.Api.Data;
using WebOfWork.Api.Integrations.Email;

namespace WebOfWork.Api.Jobs;

public class SendSupportReplyJob
{
    private readonly WebOfWorkDbContext _db;
    private readonly IEmailSender _emailSender;
    private readonly ILogger<SendSupportReplyJob> _logger;
    private readonly IEmailTemplateRenderer _templates;

    public SendSupportReplyJob(WebOfWorkDbContext db, IEmailSender emailSender, ILogger<SendSupportReplyJob> logger, IEmailTemplateRenderer templates)
    {
        _db = db;
        _emailSender = emailSender;
        _logger = logger;
        _templates = templates;
    }

    public async Task ExecuteAsync(int ticketId, CancellationToken ct = default)
    {
        var ticket = await _db.SupportTickets
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.Id == ticketId, ct);

        if (ticket == null || ticket.User == null || string.IsNullOrEmpty(ticket.User.Email))
        {
            _logger.LogWarning("SendSupportReplyJob skipped: ticket #{Id} or user email missing", ticketId);
            return;
        }

        var userEmail = ticket.User.Email;
        var userName = !string.IsNullOrEmpty(ticket.User.FullName) ? ticket.User.FullName : userEmail;
        var subject = $"Update on your Support Ticket #{ticket.Id}: {ticket.Subject}";
        var htmlBody = await _templates.RenderAsync("SupportReplyEmail.cshtml", new Dictionary<string, string>
        {
            ["UserName"] = userName,
            ["Subject"] = ticket.Subject,
            ["TicketId"] = ticket.Id.ToString(),
            ["AdminReply"] = ticket.AdminReply ?? string.Empty,
            ["Status"] = ticket.Status.ToString()
        }, ct);

        _logger.LogInformation("SendSupportReplyJob: enqueuing SMTP send to {Email} for ticket #{Id}", userEmail, ticketId);
        await _emailSender.SendAsync(userEmail, subject, htmlBody, ct);
        _logger.LogInformation("SendSupportReplyJob: SMTP sent for ticket #{Id}", ticketId);
    }

}
