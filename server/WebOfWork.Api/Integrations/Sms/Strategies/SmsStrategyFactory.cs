using System;
using System.Collections.Generic;
using WebOfWork.Api.Models.Enums;

namespace WebOfWork.Api.Integrations.Sms.Strategies;

public class GenericNotificationSmsStrategy : ISmsMessageStrategy
{
    public string GenerateMessage(params object[] args)
    {
        if (args == null || args.Length == 0) return "You have a new notification on WebOfWork.";
        return $"WebOfWork: {args[0]}";
    }
}

public class NewMessageSmsStrategy : ISmsMessageStrategy
{
    public string GenerateMessage(params object[] args)
    {
        var from = args.Length > 0 ? args[0].ToString() : "someone";
        return $"You have a new chat message from {from}. Open WebOfWork to reply.";
    }
}

public class ContractStartedSmsStrategy : ISmsMessageStrategy
{
    public string GenerateMessage(params object[] args)
    {
        return "Great news! Your contract has started on WebOfWork.";
    }
}

public class WorkDeliveredSmsStrategy : ISmsMessageStrategy
{
    public string GenerateMessage(params object[] args)
    {
        return "Work has been delivered on your WebOfWork contract. Please review.";
    }
}

public class ContractCompletedSmsStrategy : ISmsMessageStrategy
{
    public string GenerateMessage(params object[] args)
    {
        return "Your WebOfWork contract has been completed successfully.";
    }
}

public class PaymentCreditedSmsStrategy : ISmsMessageStrategy
{
    /// <remarks>
    /// Says "held in escrow", not "credited to your account".
    ///
    /// This notification is published the moment a client's payment is verified,
    /// which moves the payment to `Escrow` -- the funds are held, not paid out.
    /// The money reaches the freelancer later, when `PayoutService` releases it
    /// and publishes `PayoutReleased`. The old wording asserted something the
    /// database contradicted at this point, and a freelancer who read it and
    /// then found nothing in their balance had been told something untrue.
    ///
    /// The `args[0]` is the freelancer's share, not the gross amount, so it
    /// matches the number quoted in the in-app notification.
    /// </remarks>
    public string GenerateMessage(params object[] args)
    {
        var amount = args.Length > 0 ? args[0].ToString() : "the funds";
        return $"A payment of {amount} USD is held in escrow for you and will be released when the work is approved.";
    }
}

public class DisputeOpenedSmsStrategy : ISmsMessageStrategy
{
    public string GenerateMessage(params object[] args)
    {
        return "A dispute has been opened on your WebOfWork contract.";
    }
}

public class RevisionRequestedSmsStrategy : ISmsMessageStrategy
{
    public string GenerateMessage(params object[] args)
    {
        return "A revision has been requested on your WebOfWork delivery.";
    }
}

public class OfferReceivedSmsStrategy : ISmsMessageStrategy
{
    public string GenerateMessage(params object[] args)
    {
        // The amount was parsed and then discarded: both branches returned the same
        // string, so `out var amount` was dead and every offer SMS said "for the
        // project" no matter what was actually on offer. A recipient deciding
        // whether to accept needs the number.
        //
        // InvariantCulture is deliberate. Formatting to "1500.00" under a locale
        // that uses a comma would render 1500.50 as "1500,50" -- or, worse, as
        // 1.500,50 -- so the recipient would read the wrong figure.
        if (args.Length > 0 && decimal.TryParse(
                args[0]?.ToString(),
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture,
                out var amount))
        {
            var formatted = amount.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
            return $"You have received a new contract offer for {formatted} USD on WebOfWork!";
        }

        // No usable amount. Say the offer exists without inventing a figure.
        return "You received a new contract offer for the project.";
    }
}

public class InvitationReceivedSmsStrategy : ISmsMessageStrategy
{
    public string GenerateMessage(params object[] args)
    {
        return "You received a new invitation for the project.";
    }
}

public class ProposalAcceptedSmsStrategy : ISmsMessageStrategy
{
    public string GenerateMessage(params object[] args)
    {
        return "Your proposal for the project was accepted!";
    }
}

public class SmsStrategyFactory
{
    private readonly Dictionary<string, ISmsMessageStrategy> _strategies;

    public SmsStrategyFactory()
    {
        _strategies = new Dictionary<string, ISmsMessageStrategy>(StringComparer.OrdinalIgnoreCase)
        {
            // Ad-hoc keys, for messages sent outside the notification system (OTP,
            // transactional receipts). Anything that IS a `NotificationType` belongs
            // in the `nameof(NotificationType.*)` group below, because that is the
            // key `NotificationService` looks up.
            { "otp", new OtpSmsStrategy() },
            { "contract_offered", new ContractOfferedSmsStrategy() },
            // "payment_verified" used to be registered here. Its only caller was a
            // direct `_smsSender.SendSmsAsync` in `PaymentService`, which bypassed
            // notification preferences and duplicated the `PaymentCredited`
            // notification. That call is gone, so this key was unreachable --
            // removed rather than left as a trap for the next person to wire up
            // the same bypass.
            { nameof(NotificationType.ContractStarted), new ContractStartedSmsStrategy() },
            { nameof(NotificationType.WorkDelivered), new WorkDeliveredSmsStrategy() },
            { nameof(NotificationType.ContractCompleted), new ContractCompletedSmsStrategy() },
            { nameof(NotificationType.PaymentCredited), new PaymentCreditedSmsStrategy() },
            { nameof(NotificationType.NewMessage), new NewMessageSmsStrategy() },
            { nameof(NotificationType.DisputeOpened), new DisputeOpenedSmsStrategy() },
            { nameof(NotificationType.RevisionRequested), new RevisionRequestedSmsStrategy() },
            { nameof(NotificationType.OfferReceived), new OfferReceivedSmsStrategy() },
            { nameof(NotificationType.InvitationReceived), new InvitationReceivedSmsStrategy() },
            { nameof(NotificationType.ProposalAccepted), new ProposalAcceptedSmsStrategy() },
            // `PayoutReleased` used to be registered above as "payout_released",
            // with the ad-hoc keys. It is a `NotificationType`, and
            // `NotificationService` looks strategies up by `type.ToString()` --
            // "PayoutReleased" -- so that key was unreachable from the dispatcher,
            // which fell through to `GenericNotificationSmsStrategy` and sent
            // generic text.
            //
            // The tailored message only reached users at all because `PayoutService`
            // sent a *second* SMS directly, bypassing their preferences: a
            // freelancer with SMS switched off still got payout texts. Registering
            // it correctly and deleting the direct send fixes both problems.
            { nameof(NotificationType.PayoutReleased), new PayoutReleasedSmsStrategy() }
        };
    }

    public ISmsMessageStrategy GetStrategy(string notificationType)
    {
        if (_strategies.TryGetValue(notificationType, out var strategy))
            return strategy;

        throw new ArgumentException($"Unknown notification type: {notificationType}");
    }

    public bool TryGetValue(string notificationType, out ISmsMessageStrategy strategy)
    {
        return _strategies.TryGetValue(notificationType, out strategy!);
    }

    public bool TryGetValueFallback(NotificationType type, out ISmsMessageStrategy strategy)
    {
        if (_strategies.TryGetValue(type.ToString(), out var found))
        {
            strategy = found;
            return true;
        }
        strategy = new GenericNotificationSmsStrategy();
        return true;
    }
}

