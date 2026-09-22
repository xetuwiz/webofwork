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

    public SendSupportReplyJob(WebOfWorkDbContext db, IEmailSender emailSender, ILogger<SendSupportReplyJob> logger)
    {
        _db = db;
        _emailSender = emailSender;
        _logger = logger;
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
        var htmlBody = BuildHtmlBody(userName, ticket.Id, ticket.Subject, ticket.AdminReply ?? string.Empty, ticket.Status.ToString());

        _logger.LogInformation("SendSupportReplyJob: enqueuing SMTP send to {Email} for ticket #{Id}", userEmail, ticketId);
        await _emailSender.SendAsync(userEmail, subject, htmlBody, ct);
        _logger.LogInformation("SendSupportReplyJob: SMTP sent for ticket #{Id}", ticketId);
    }

    private static string BuildHtmlBody(string name, int id, string subject, string reply, string status)
    {
        var safeReply = System.Net.WebUtility.HtmlEncode(reply).Replace("\n", "<br/>");
        return $@"<!DOCTYPE html>
<html>
<head>
<meta charset=""utf-8""/>
<style>
body {{ font-family: Arial, sans-serif; background-color: #f9fafb; padding: 20px; }}
.wrapper {{ max-width: 600px; margin: auto; background: #ffffff; padding: 30px; border-radius: 12px; border: 1px solid #e5e7eb; }}
h2 {{ color: #111827; border-bottom: 2px solid #2563eb; padding-bottom: 10px; }}
.quote {{ background-color: #f3f4f6; padding: 20px; border-left: 4px solid #2563eb; margin: 20px 0; border-radius: 4px; }}
.quote p {{ margin: 0; color: #4b5563; line-height: 1.6; }}
.footer {{ margin-top: 30px; border-top: 1px solid #f3f4f6; padding-top: 20px; color: #9ca3af; font-size: 12px; }}
</style>
</head>
<body>
  <div class=""wrapper"">
    <h2>Support Ticket Update</h2>
    <p style=""color: #374151; font-size: 16px;"">Hello <strong>{System.Net.WebUtility.HtmlEncode(name)}</strong>,</p>
    <p style=""color: #374151;"">Our support team has responded to your ticket regarding <strong>""{System.Net.WebUtility.HtmlEncode(subject)}""</strong> (Ticket #{id}).</p>
    <div class=""quote"">
      <p style=""font-weight: bold; color: #1f2937; margin-bottom: 8px;"">Admin Response:</p>
      <p>{safeReply}</p>
    </div>
    <p style=""font-size: 14px; color: #6b7280;"">Current Status: <span style=""font-weight: bold; color: #2563eb;"">{status}</span></p>
    <p class=""footer"">— WebOfWork Support Team</p>
  </div>
</body>
</html>";
    }
}
