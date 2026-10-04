using WebOfWork.Api.Integrations.Email;

namespace WebOfWork.Api.Tests;

/// <summary>
/// An <see cref="IEmailSender"/> that records what it was asked to send and
/// sends nothing.
/// </summary>
/// <remarks>
/// <para>
/// This lives in the test project rather than in production, deliberately.
/// <c>NotificationService</c> now dispatches business notifications over email,
/// so every test that constructs one has to supply a sender. The alternative is
/// a <c>NullLogger</c>-style no-op in <c>server/</c> that exists only to satisfy
/// test constructors -- production code carrying a dependency nothing in
/// production uses.
/// </para>
/// <para>
/// It records rather than discards, so a test can assert that an email was (or
/// was not) sent. A silently-discarding double would let a regression where the
/// email channel stops firing pass unnoticed, which is exactly the failure this
/// notification work exists to prevent.
/// </para>
/// <para>
/// <see cref="Sent"/> is a plain list rather than a concurrent collection
/// because the service under test dispatches channels sequentially within a
/// single call, and xUnit runs each test on one thread.
/// </para>
/// </remarks>
public sealed class RecordingEmailSender : IEmailSender
{
    /// <summary>One recorded send.</summary>
    public sealed record SentEmail(string To, string Subject, string HtmlBody);

    /// <summary>Everything this sender was asked to deliver, in order.</summary>
    public List<SentEmail> Sent { get; } = new();

    /// <summary>Convenience for the common "was anything sent at all" assertion.</summary>
    public int Count => Sent.Count;

    public Task SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default)
    {
        Sent.Add(new SentEmail(to, subject, htmlBody));
        return Task.CompletedTask;
    }

    /// <summary>Forgets everything recorded so far.</summary>
    public void Clear() => Sent.Clear();
}
