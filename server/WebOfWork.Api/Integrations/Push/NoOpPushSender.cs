namespace WebOfWork.Api.Integrations.Push;

/// <summary>
/// The transport used when no Firebase credentials are configured.
///
/// Registered instead of <see cref="FirebasePushSender"/> so the notification
/// pipeline has exactly one code path: the service asks <see cref="IPushSender"/>
/// to deliver and gets "not configured" back. A null-guarded second path in
/// <c>NotificationService</c> would be a branch that never gets exercised in
/// development and is therefore never tested.
///
/// Everything except push still works -- the notification row, the WebSocket
/// push, and SMS.
/// </summary>
public sealed class NoOpPushSender : IPushSender
{
    public bool IsConfigured => false;

    public Task<IReadOnlyList<PushResult>> SendAsync(
        IReadOnlyCollection<PushMessage> messages,
        CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<PushResult>>(
            messages.Select(m => new PushResult(m.Token, false, Error: "push not configured")).ToList());
}
