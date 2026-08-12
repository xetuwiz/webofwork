namespace WebOfWork.Api.Integrations.Email;

/// <summary>
/// Abstraction over the SMTP sender.
/// Registered as a singleton and injected into Hangfire background jobs.
/// </summary>
public interface IEmailSender
{
    /// <summary>Send an HTML email. Called from Hangfire jobs — never from controllers directly.</summary>
    Task SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default);
}
