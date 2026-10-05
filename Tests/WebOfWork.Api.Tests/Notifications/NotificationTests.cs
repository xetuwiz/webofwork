using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using WebOfWork.Api.Data;
using WebOfWork.Api.Integrations.Push;
using WebOfWork.Api.Integrations.Sms;
using WebOfWork.Api.Integrations.Sms.Strategies;
using WebOfWork.Api.Middleware;
using WebOfWork.Api.Models;
using WebOfWork.Api.Models.Enums;
using WebOfWork.Api.Services;
using Moq;
using Microsoft.Extensions.Logging.Abstractions;

namespace WebOfWork.Api.Tests.Notifications;

[Trait("Category", "Container")]
public class NotificationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:15-alpine")
        .WithDatabase("webofwork_notif_test")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    public async Task InitializeAsync() => await _postgres.StartAsync();
    public async Task DisposeAsync() => await _postgres.DisposeAsync();

    private async Task<WebOfWorkDbContext> GetDbContextAsync()
    {
        var options = new DbContextOptionsBuilder<WebOfWorkDbContext>()
            .UseSnakeCaseNamingConvention().UseNpgsql(_postgres.GetConnectionString())
            .Options;
        var db = new WebOfWorkDbContext(options);
        await db.Database.EnsureCreatedAsync();
        return db;
    }

    private async Task<(User Owner, User OtherUser)> SeedUsersAsync(WebOfWorkDbContext db)
    {
        // Both role columns are set, because that is how `AuthService` creates
        // users. Setting only `Role` leaves `CurrentRole` at `(UserRole)0`, which
        // the notification catalogue treats as "applies to nobody" -- so nothing
        // is delivered, and the publish log blames the user's own settings.
        var owner = new User { Id = Guid.NewGuid(), Email = $"o{Guid.NewGuid():N}@t.com", FullName = "O",
                               PasswordHash = "x", Role = UserRole.Freelancer, CurrentRole = UserRole.Freelancer, Phone = "+00000000009" };
        var other = new User { Id = Guid.NewGuid(), Email = $"x{Guid.NewGuid():N}@t.com", FullName = "X",
                               PasswordHash = "x", Role = UserRole.Client, CurrentRole = UserRole.Client };
        db.Users.AddRange(owner, other);
        await db.SaveChangesAsync();
        return (owner, other);
    }

    private NotificationService CreateNotificationService(WebOfWorkDbContext db, ISmsSender? sms = null)
    {
        sms ??= new Mock<ISmsSender>().Object;
        var factory = new SmsStrategyFactory();

        // The service takes the socket registry (to fan a push out to connected
        // clients) and the preference service (to honour per-channel switches and
        // per-type mutes). Both are exercised with empty/no-op instances: these
        // tests are about recipient-only access, not about delivery, and a real
        // preference service here would need its own DbContext scope.
        var preferences = new NotificationPreferenceService(
            db,
            new NoOpPushSender(),
            new NullLogger<NotificationPreferenceService>());

        // The email sender sits between the SMS factory and the logger. It is
        // a real `NotificationService` field now because business notifications
        // are dispatched over email, so it cannot be left out. These tests are
        // about recipient-only access, so nothing here should reach it.
        return new NotificationService(
            db,
            sms,
            factory,
            new RecordingEmailSender(),
            new NullLogger<NotificationService>(),
            new WebSocketGroupRegistry(),
            preferences);
    }

    // ──────────────────────────────────────────────────────────────────
    // 14.29  recipient-only access  →  403 UnauthorizedAccessException
    // ──────────────────────────────────────────────────────────────────
    [Fact]
    public async Task MarkAsReadAsync_Throws403_ForNonRecipient()
    {
        await using var db = await GetDbContextAsync();
        var (owner, other) = await SeedUsersAsync(db);
        var svcOwner = CreateNotificationService(db);

        var n = (await svcOwner.PublishAsync(owner.Id, NotificationType.NewMessage, "m1",
                       relatedEntityType: "ChatRoom", relatedEntityId: 1));
        await db.Entry(n).ReloadAsync();

        var svcOther = CreateNotificationService(db);
        var act = () => svcOther.MarkAsReadAsync(other.Id, n.Id);

        (await act.Should().ThrowExactlyAsync<UnauthorizedAccessException>())
            .Which.Message.Should().Contain("belongs to another user");
    }

    [Fact]
    public async Task DeleteNotificationAsync_Throws403_ForNonRecipient()
    {
        await using var db = await GetDbContextAsync();
        var (owner, other) = await SeedUsersAsync(db);
        var svcOwner = CreateNotificationService(db);

        var n = await svcOwner.PublishAsync(owner.Id, NotificationType.NewMessage, "m1");

        var svcOther = CreateNotificationService(db);
        var act = () => svcOther.DeleteNotificationAsync(other.Id, n.Id);

        await act.Should().ThrowExactlyAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task GetNotificationsAsync_OnlyReturnsOwnRows()
    {
        await using var db = await GetDbContextAsync();
        var (owner, other) = await SeedUsersAsync(db);
        var svcOwner = CreateNotificationService(db);
        var svcOther = CreateNotificationService(db);

        await svcOwner.PublishAsync(owner.Id, NotificationType.NewMessage, "for owner 1");
        await svcOwner.PublishAsync(owner.Id, NotificationType.NewMessage, "for owner 2");
        // `other` is a Client, so it gets a type the catalogue allows for a
        // client. This used to publish `PayoutReleased`, which is freelancer-only:
        // under the role-aware catalogue the client correctly receives nothing, so
        // the assertion below was measuring the role filter rather than the
        // recipient isolation this test is named for.
        await svcOther.PublishAsync(other.Id, NotificationType.ContractCompleted, "for other", smsArgs: 100m);

        var ownerList = await svcOwner.GetNotificationsAsync(owner.Id);
        var otherList = await svcOther.GetNotificationsAsync(other.Id);

        ownerList.Items.Should().HaveCount(2);
        ownerList.Items.All(i => i.Message.StartsWith("for owner")).Should().BeTrue();
        otherList.Items.Should().HaveCount(1);
        otherList.Items.Single().Message.Should().Be("for other");
    }

    // ──────────────────────────────────────────────────────────────────
    // 14.28c  part 1 — bulk mark-all-read works
    // ──────────────────────────────────────────────────────────────────
    [Fact]
    public async Task MarkAllAsReadAsync_BulkUpdatesOwnUnread_ReturnsCount()
    {
        await using var db = await GetDbContextAsync();
        var (owner, other) = await SeedUsersAsync(db);
        var svc = CreateNotificationService(db);

        await svc.PublishAsync(owner.Id, NotificationType.NewMessage, "a");
        await svc.PublishAsync(owner.Id, NotificationType.NewMessage, "b");
        await svc.PublishAsync(owner.Id, NotificationType.NewMessage, "c");
        await svc.PublishAsync(other.Id, NotificationType.NewMessage, "other's");

        var before = await svc.GetNotificationsAsync(owner.Id);
        before.UnreadCount.Should().Be(3);

        var marked = await svc.MarkAllAsReadAsync(owner.Id);
        marked.Should().Be(3);

        var after = await svc.GetNotificationsAsync(owner.Id);
        after.UnreadCount.Should().Be(0);
        after.Items.All(i => i.IsRead).Should().BeTrue();

        var otherList = await svc.GetNotificationsAsync(other.Id);
        otherList.UnreadCount.Should().Be(1);
    }

    // ──────────────────────────────────────────────────────────────────
    // 14.28c  part 2 — delete notification works (recipient only, hard remove)
    // ──────────────────────────────────────────────────────────────────
    [Fact]
    public async Task DeleteNotificationAsync_RemovesRow_ForRecipient()
    {
        await using var db = await GetDbContextAsync();
        var (owner, _) = await SeedUsersAsync(db);
        var svc = CreateNotificationService(db);

        var n = await svc.PublishAsync(owner.Id, NotificationType.NewMessage, "to-delete");
        var before = await svc.GetNotificationsAsync(owner.Id);
        before.TotalCount.Should().Be(1);

        await svc.DeleteNotificationAsync(owner.Id, n.Id);

        var after = await svc.GetNotificationsAsync(owner.Id);
        after.TotalCount.Should().Be(0);
        (await db.Notifications.FindAsync(n.Id)).Should().BeNull();
    }

    // ──────────────────────────────────────────────────────────────────
    // 14.30  test notification creation on domain events
    // ──────────────────────────────────────────────────────────────────
    [Fact]
    public async Task TriggerFromDomainEventAsync_ContractStarted_CreatesNotificationRow()
    {
        await using var db = await GetDbContextAsync();
        var (owner, _) = await SeedUsersAsync(db);
        var svc = CreateNotificationService(db);

        var evt = new ContractStartedEvent(owner.Id, ContractId: 42);
        await svc.TriggerFromDomainEventAsync(evt);

        var list = await svc.GetNotificationsAsync(owner.Id);
        list.Items.Should().HaveCount(1);
        var n = list.Items.Single();
        // The response carries the wire name ("contract_started"), not the CLR
        // enum name. Derived via NotificationWireFormat so the assertion states the
        // contract rather than hardcoding the string a second time.
        n.Type.Should().Be(NotificationWireFormat.ToWireFormat(NotificationType.ContractStarted));
        n.RelatedEntityType.Should().Be(nameof(Contract));
        n.RelatedEntityId.Should().Be(42);
    }

    [Fact]
    public async Task TriggerFromDomainEventAsync_PaymentCredited_CreatesRowAndPassesSmsArgs()
    {
        await using var db = await GetDbContextAsync();
        var (owner, _) = await SeedUsersAsync(db);

        // SMS is off by default, so the recipient has to opt in before this can
        // assert on SMS delivery. See `NotificationCatalogue.DefaultFor`.
        db.NotificationPreferences.Add(new NotificationPreference
        {
            UserId = owner.Id,
            SmsEnabled = true,
        });
        await db.SaveChangesAsync();

        var mockSms = new Mock<ISmsSender>();
        var svc = CreateNotificationService(db, mockSms.Object);

        await svc.TriggerFromDomainEventAsync(new PaymentCreditedEvent(owner.Id, PaymentId: 7, Amount: 250.50m));

        var list = await svc.GetNotificationsAsync(owner.Id);
        list.Items.Should().HaveCount(1);
        var n = list.Items.Single();
        n.Type.Should().Be(NotificationWireFormat.ToWireFormat(NotificationType.PaymentCredited));
        n.RelatedEntityType.Should().Be(nameof(Payment));
        n.RelatedEntityId.Should().Be(7);
        mockSms.Verify(s => s.SendSmsAsync(
            It.Is<string>(p => p == owner.Phone),
            It.Is<string>(t => t.Contains("250.50"))), Times.Once);
    }

    [Fact]
    public async Task TriggerFromDomainEventAsync_All11EventTypes_ProduceRows()
    {
        await using var db = await GetDbContextAsync();
        var (owner, _) = await SeedUsersAsync(db);
        var svc = CreateNotificationService(db);

        var events = new object[]
        {
            new ContractStartedEvent(owner.Id, 1),
            new WorkDeliveredEvent(owner.Id, 2),
            new ContractCompletedEvent(owner.Id, 3),
            new PaymentCreditedEvent(owner.Id, 4, 10m),
            new PayoutReleasedEvent(owner.Id, 5, 20m),
            new DisputeOpenedEvent(owner.Id, 6),
            new RevisionRequestedEvent(owner.Id, 7),
            new OfferReceivedEvent(owner.Id, 8),
            new InvitationReceivedEvent(owner.Id, 9),
            new ProposalAcceptedEvent(owner.Id, 10),
        };

        foreach (var e in events)
        {
            var trigger = typeof(INotificationPublisher).GetMethod(nameof(INotificationPublisher.TriggerFromDomainEventAsync))!
                              .MakeGenericMethod(e.GetType());
            await (Task)trigger.Invoke(svc, new[] { e, CancellationToken.None })!;
        }

        var list = await svc.GetNotificationsAsync(owner.Id, isRead: null, limit: 50);
        list.TotalCount.Should().Be(10);
        list.Items.Select(i => i.Type).Distinct().Should().HaveCount(10);
    }

    // ──────────────────────────────────────────────────────────────────
    // 14.32a  SMS follows the catalogue, not a hand-kept allow-list
    // ──────────────────────────────────────────────────────────────────
    //
    // This test used to be `PublishAsync_SmsDispatched_ForNewMessageType_WhenPhonePresent`
    // and asserted that a chat message DID produce an SMS. `NotificationCatalogue`
    // now marks `new_message` in-app only, as a product decision -- an SMS per
    // chat message is expensive noise. (`NewMessageSmsStrategy` does exist; the
    // capability is deliberately unused, not missing.)
    //
    // So the expectation is inverted, and the type is a poor choice for a
    // positive test: an in-app-only type can never produce an SMS no matter how
    // the code is wired. The positive case below uses a type the catalogue
    // actually allows over SMS.
    [Fact]
    public async Task PublishAsync_NoSmsSent_ForInAppOnlyType_EvenWithPhoneAndSmsArgs()
    {
        await using var db = await GetDbContextAsync();
        var (owner, _) = await SeedUsersAsync(db);
        var mockSms = new Mock<ISmsSender>();
        var svc = CreateNotificationService(db, mockSms.Object);

        await svc.PublishAsync(owner.Id, NotificationType.NewMessage,
            "msg text", relatedEntityType: "ChatRoom", relatedEntityId: 5,
            smsArgs: "Alice Client");

        // The row is still written -- in-app delivery is what the catalogue
        // permits, and it is unaffected.
        var row = await db.Notifications.SingleAsync(n => n.RecipientId == owner.Id);
        row.Type.Should().Be(NotificationType.NewMessage);

        mockSms.Verify(s => s.SendSmsAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task PublishAsync_SmsDispatched_ForCatalogueSmsType_WhenPhonePresent()
    {
        await using var db = await GetDbContextAsync();
        var (owner, _) = await SeedUsersAsync(db);

        // SMS is off by default (`NotificationCatalogue.DefaultFor`), because it
        // costs money per message. Without this row the publish is correctly
        // silent, which is the behaviour the default exists to produce -- so the
        // test has to opt in before it can assert anything about delivery.
        db.NotificationPreferences.Add(new NotificationPreference
        {
            UserId = owner.Id,
            SmsEnabled = true,
        });
        await db.SaveChangesAsync();

        var mockSms = new Mock<ISmsSender>();
        var svc = CreateNotificationService(db, mockSms.Object);

        // Guard the premise rather than trusting it: if the catalogue ever stops
        // allowing SMS for this type, this test should fail with a clear message
        // instead of a confusing "no SMS was sent" on the Verify below.
        var descriptor = NotificationCatalogue.Find(
            NotificationWireFormat.ToWireFormat(NotificationType.ContractStarted));
        descriptor.Should().NotBeNull(
            "ContractStarted should have a catalogue descriptor; if the catalogue changed, pick another type here");
        NotificationCatalogue.Supports(descriptor!, NotificationChannel.Sms).Should().BeTrue(
            "this test only makes sense for a type the catalogue allows over SMS");

        await svc.PublishAsync(owner.Id, NotificationType.ContractStarted,
            "Your contract has started", relatedEntityType: "Contract", relatedEntityId: 9);

        mockSms.Verify(s => s.SendSmsAsync(
            It.Is<string>(p => p == owner.Phone),
            It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task PublishAsync_NoSmsSent_WhenPhoneMissing()
    {
        await using var db = await GetDbContextAsync();
        var noPhone = new User { Id = Guid.NewGuid(), Email = "n@t.com", FullName = "NoPhone",
                                 PasswordHash = "x", Role = UserRole.Client, CurrentRole = UserRole.Client, Phone = null };
        db.Users.Add(noPhone);
        await db.SaveChangesAsync();

        var mockSms = new Mock<ISmsSender>();
        var svc = CreateNotificationService(db, mockSms.Object);

        await svc.PublishAsync(noPhone.Id, NotificationType.NewMessage,
            "msg text", relatedEntityType: "ChatRoom", relatedEntityId: 1, smsArgs: "Sender");

        mockSms.Verify(s => s.SendSmsAsync(It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
    }
}
