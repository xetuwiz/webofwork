using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
using MimeKit.Text;
using WebOfWork.Api.Configuration;

namespace WebOfWork.Api.Integrations.Email;

/// <summary>
/// MailKit SMTP sender.
/// SMTP server: mail.xetuwiz.de:587 with STARTTLS.
/// From: no-reply@xetuwiz.de / "WebOfWork".
///
/// Ported from:
///   - demo2/application.properties (spring.mail.host/port/starttls)
///   - demo2/MailConfig.java (JavaMailSender configuration)
/// Do NOT use Gmail SMTP — that was the Grolance legacy setting.
/// </summary>
public sealed class SmtpEmailSender : IEmailSender
{
    private readonly MailOptions _options;
    private readonly ILogger<SmtpEmailSender> _logger;

    public SmtpEmailSender(IOptions<MailOptions> options, ILogger<SmtpEmailSender> logger)
    {
        _options = options.Value;
        _logger  = logger;
    }

    public async Task SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_options.FromName, _options.FromAddress));
        message.To.Add(MailboxAddress.Parse(to));
        message.Subject = subject;
        message.Body    = new TextPart(TextFormat.Html) { Text = htmlBody };

        using var client = new SmtpClient();

        try
        {
            // STARTTLS on port 587 — matches application.properties and plan spec.
            await client.ConnectAsync(
                _options.Host,
                _options.Port,
                SecureSocketOptions.StartTls,
                ct);

            await client.AuthenticateAsync(_options.Username, _options.Password, ct);
            await client.SendAsync(message, ct);

            _logger.LogInformation(
                "Email sent. To: {To} Subject: {Subject}",
                to, subject);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to send email. To: {To} Subject: {Subject} Host: {Host}:{Port}",
                to, subject, _options.Host, _options.Port);
            throw;
        }
        finally
        {
            await client.DisconnectAsync(true, ct);
        }
    }
}
