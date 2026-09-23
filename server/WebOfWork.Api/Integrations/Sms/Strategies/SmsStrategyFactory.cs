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
    public string GenerateMessage(params object[] args)
    {
        var amount = args.Length > 0 ? args[0].ToString() : "the funds";
        return $"A payment of {amount} USD has been credited to your WebOfWork account.";
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
        if (args.Length > 0 && decimal.TryParse(args[0]?.ToString(), out var amount))
            return $"You have received a new contract offer for {amount:0.00} USD on WebOfWork!";

        return "You have received a new offer on WebOfWork.";
    }
}

public class InvitationReceivedSmsStrategy : ISmsMessageStrategy
{
    public string GenerateMessage(params object[] args)
    {
        return "You have received a new project invitation on WebOfWork.";
    }
}

public class ProposalAcceptedSmsStrategy : ISmsMessageStrategy
{
    public string GenerateMessage(params object[] args)
    {
        return "Congratulations! Your proposal has been accepted on WebOfWork.";
    }
}

public class SmsStrategyFactory
{
    private readonly Dictionary<string, ISmsMessageStrategy> _strategies;

    public SmsStrategyFactory()
    {
        _strategies = new Dictionary<string, ISmsMessageStrategy>(StringComparer.OrdinalIgnoreCase)
        {
            { "otp", new OtpSmsStrategy() },
            { "contract_offered", new ContractOfferedSmsStrategy() },
            { "payment_verified", new PaymentVerifiedSmsStrategy() },
            { "payout_released", new PayoutReleasedSmsStrategy() },
            { nameof(NotificationType.ContractStarted), new ContractStartedSmsStrategy() },
            { nameof(NotificationType.WorkDelivered), new WorkDeliveredSmsStrategy() },
            { nameof(NotificationType.ContractCompleted), new ContractCompletedSmsStrategy() },
            { nameof(NotificationType.PaymentCredited), new PaymentCreditedSmsStrategy() },
            { nameof(NotificationType.NewMessage), new NewMessageSmsStrategy() },
            { nameof(NotificationType.DisputeOpened), new DisputeOpenedSmsStrategy() },
            { nameof(NotificationType.RevisionRequested), new RevisionRequestedSmsStrategy() },
            { nameof(NotificationType.OfferReceived), new OfferReceivedSmsStrategy() },
            { nameof(NotificationType.InvitationReceived), new InvitationReceivedSmsStrategy() },
            { nameof(NotificationType.ProposalAccepted), new ProposalAcceptedSmsStrategy() }
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
