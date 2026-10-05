using System;
using Xunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using WebOfWork.Api.Integrations.Sms.Strategies;
using WebOfWork.Api.Models.Enums;

namespace WebOfWork.Api.Tests.Sms;

public class SmsStrategyFactoryTests
{
    [Fact]
    public void Factory_ShouldReturnCorrectStrategy()
    {
        var services = new ServiceCollection();
        services.AddTransient<OtpSmsStrategy>();
        services.AddTransient<ContractOfferedSmsStrategy>();
        var provider = services.BuildServiceProvider();
        var factory = new SmsStrategyFactory();

        var strategy = factory.GetStrategy("otp");
        strategy.Should().BeOfType<OtpSmsStrategy>();
    }

    [Fact]
    public void OfferReceivedStrategy_IncludesUsdAmountForContractOffers()
    {
        var factory = new SmsStrategyFactory();

        var message = factory.GetStrategy("OfferReceived").GenerateMessage(1500m);

        message.Should().Be("You have received a new contract offer for 1500.00 USD on WebOfWork!");
    }

    /// <summary>
    /// The dispatcher resolves strategies by <c>NotificationType.ToString()</c>.
    /// A strategy registered under any other key is invisible to it, and the
    /// publish silently degrades to <c>GenericNotificationSmsStrategy</c>.
    /// </summary>
    /// <remarks>
    /// <c>PayoutReleased</c> was registered as <c>"payout_released"</c> and so was
    /// unreachable. Nothing failed: the generic fallback produced a valid message,
    /// so the only symptom was that users got blunter text than intended -- and
    /// the tailored version only appeared because <c>PayoutService</c> sent a
    /// second SMS directly, ignoring their preferences.
    ///
    /// This asserts the key, not the message, so the failure mode cannot come
    /// back quietly. <c>TryGetValue</c> is the exact call
    /// <c>NotificationService</c> makes.
    /// </remarks>
    [Theory]
    [InlineData(NotificationType.ContractStarted)]
    [InlineData(NotificationType.WorkDelivered)]
    [InlineData(NotificationType.ContractCompleted)]
    [InlineData(NotificationType.PaymentCredited)]
    [InlineData(NotificationType.NewMessage)]
    [InlineData(NotificationType.DisputeOpened)]
    [InlineData(NotificationType.RevisionRequested)]
    [InlineData(NotificationType.OfferReceived)]
    [InlineData(NotificationType.InvitationReceived)]
    [InlineData(NotificationType.ProposalAccepted)]
    [InlineData(NotificationType.PayoutReleased)]
    public void EverySmsCapableNotificationType_ResolvesADedicatedStrategy(NotificationType type)
    {
        var factory = new SmsStrategyFactory();

        var resolved = factory.TryGetValue(type.ToString(), out var strategy);

        resolved.Should().BeTrue(
            $"{type} is published over SMS, so the dispatcher -- which looks up " +
            $"type.ToString() = \"{type}\" -- must find a dedicated strategy rather " +
            "than falling through to the generic one");
        strategy.Should().NotBeOfType<GenericNotificationSmsStrategy>(
            $"{type} has a tailored strategy; reaching the generic fallback means the " +
            "registration key does not match what the dispatcher looks up");
    }

    [Fact]
    public void PayoutReleasedStrategy_IncludesReleasedAmount()
    {
        var factory = new SmsStrategyFactory();

        // Keyed the way the dispatcher looks it up, not the way it used to be
        // registered.
        var message = factory.GetStrategy(nameof(NotificationType.PayoutReleased))
                             .GenerateMessage(90m, 42);

        message.Should().Contain("90.00");
    }

    /// <summary>
    /// Every <c>NotificationType</c> a user should get tailored text for must be
    /// reachable by its own name, because that is the only key
    /// <c>NotificationService</c> looks up.
    /// </summary>
    /// <remarks>
    /// This replaced a test that compared a hardcoded list of ad-hoc keys against
    /// the enum names. That test could not have caught the bug it was written
    /// for: <c>"payout_released"</c> and <c>PayoutReleased</c> differ by an
    /// underscore, so a name comparison said they were unrelated and passed. And
    /// the hardcoded list had to be edited by hand whenever a key was added,
    /// which is why it still listed <c>payment_verified</c> after that key was
    /// removed as unreachable.
    ///
    /// Asking the factory the question the dispatcher actually asks cannot drift.
    /// </remarks>
    [Theory]
    [InlineData(NotificationType.ContractStarted)]
    [InlineData(NotificationType.WorkDelivered)]
    [InlineData(NotificationType.ContractCompleted)]
    [InlineData(NotificationType.PaymentCredited)]
    [InlineData(NotificationType.NewMessage)]
    [InlineData(NotificationType.DisputeOpened)]
    [InlineData(NotificationType.RevisionRequested)]
    [InlineData(NotificationType.OfferReceived)]
    [InlineData(NotificationType.InvitationReceived)]
    [InlineData(NotificationType.ProposalAccepted)]
    [InlineData(NotificationType.PayoutReleased)]
    public void EveryTailoredNotificationType_IsReachableByItsOwnName(NotificationType type)
    {
        var factory = new SmsStrategyFactory();

        factory.TryGetValue(type.ToString(), out var strategy).Should().BeTrue(
            $"NotificationService looks strategies up by type.ToString() = \"{type}\", " +
            "so a strategy registered under any other key is unreachable and the " +
            "message silently degrades to the generic one");

        strategy.Should().NotBeOfType<GenericNotificationSmsStrategy>(
            $"\"{type}\" resolved to the generic fallback, so the tailored text " +
            "never reaches the user");
    }

    /// <summary>
    /// The factory's dictionary is <c>OrdinalIgnoreCase</c>, so an ad-hoc key
    /// that differs from a <c>NotificationType</c> name only by case would
    /// shadow it, and the tailored message would never be reached.
    /// </summary>
    /// <remarks>
    /// This is the check the previous version of this file was reaching for, and
    /// it is the one that matters: it compares using the same comparer the
    /// dictionary uses. The old test used <c>Enum.GetNames</c> and an ordinal
    /// comparison against a hand-maintained key list, which could not detect a
    /// case collision and had to be edited by hand -- it still listed
    /// <c>payment_verified</c> after that key was removed as unreachable.
    ///
    /// Note it does <em>not</em> claim every <c>NotificationType</c> has a
    /// strategy. Seven of the eighteen deliberately do not
    /// (<c>ProposalRejected</c>, <c>ReviewReceived</c>, <c>SupportReply</c> and
    /// friends) and fall through to the generic text. The theory above pins the
    /// eleven that must resolve.
    /// </remarks>
    [Fact]
    public void AdHocKeys_CannotShadowANotificationTypeName()
    {
        var factory = new SmsStrategyFactory();

        // The ad-hoc keys, for traffic genuinely outside the notification
        // system: an OTP and a transactional offer receipt.
        var adHocKeys = new[] { "otp", "contract_offered" };

        foreach (var key in adHocKeys)
        {
            factory.TryGetValue(key, out _).Should().BeTrue(
                $"the ad-hoc key \"{key}\" is expected to resolve");

            var shadowed = Enum.GetNames<NotificationType>()
                .FirstOrDefault(name => string.Equals(name, key, StringComparison.OrdinalIgnoreCase));

            shadowed.Should().BeNull(
                $"\"{key}\" differs from the NotificationType \"{shadowed}\" only by case, " +
                "and the factory dictionary is OrdinalIgnoreCase -- so the ad-hoc " +
                "entry wins and the tailored message is unreachable");
        }
    }
}
