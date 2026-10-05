using System.Collections.Generic;

namespace WebOfWork.Api.Integrations.Push;

/// <summary>One push to deliver to one device.</summary>
/// <param name="Token">The device's FCM registration token.</param>
/// <param name="Title">Shade title. May be null for a data-only message.</param>
/// <param name="Body">Shade body / the message text.</param>
/// <param name="Data">
/// String-only key/values forwarded to the client. FCM rejects a non-string
/// value outright, so this is a string map by construction rather than
/// <c>object</c> converted at the edge.
/// </param>
/// <param name="Route">
/// Logical destination the client maps to a screen, e.g. "chat" or "notifications".
/// Kept out of the title/body so the client can navigate without parsing English.
/// </param>
public sealed record PushMessage(
    string Token,
    string? Title,
    string? Body,
    IReadOnlyDictionary<string, string>? Data = null,
    string? Route = null);

/// <summary>Result of a send attempt, per device.</summary>
/// <param name="Token">The token this result is about.</param>
/// <param name="Delivered">True when FCM accepted the message.</param>
/// <param name="Invalidated">
/// True when FCM reported the token as permanently unusable
/// (<c>UNREGISTERED</c> / <c>INVALID_ARGUMENT</c>). The caller must delete the row:
/// a stale token fails forever, and retrying it on every future notification is
/// how a token table rots.
/// </param>
/// <param name="Error">Diagnostic detail for the log. Never surfaced to clients.</param>
public sealed record PushResult(
    string Token,
    bool Delivered,
    bool Invalidated = false,
    string? Error = null);

/// <summary>
/// Abstraction over the push transport.
///
/// Registered as a singleton, exactly like <c>IEmailSender</c>: it holds a
/// <see cref="System.Net.Http.HttpClient"/> and a cached OAuth token, neither of
/// which should be rebuilt per notification.
///
/// Implementations must never throw for a delivery failure. A push that cannot be
/// sent is a lost notification, not a failed request -- the notification row is
/// already written, and turning a push failure into an exception would roll back
/// or 500 a write the user actually wanted.
/// </summary>
public interface IPushSender
{
    /// <summary>True when a real transport is configured and the server may attempt delivery.</summary>
    bool IsConfigured { get; }

    Task<IReadOnlyList<PushResult>> SendAsync(
        IReadOnlyCollection<PushMessage> messages,
        CancellationToken ct = default);
}
