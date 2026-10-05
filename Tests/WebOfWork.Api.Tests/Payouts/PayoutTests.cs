using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using WebOfWork.Api.Data;
using WebOfWork.Api.DTOs.Payouts;
using WebOfWork.Api.Models;
using WebOfWork.Api.Models.Enums;
using WebOfWork.Api.Services;
using WebOfWork.Api.Integrations.PayPal;
using WebOfWork.Api.Integrations.Sms;
using Xunit;
using Moq;
using Testcontainers.PostgreSql;

namespace WebOfWork.Api.Tests.Payouts;

[Trait("Category", "Container")]
public class PayoutTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:15-alpine")
        .Build();

    public async Task InitializeAsync() => await _postgres.StartAsync();
    public async Task DisposeAsync()    => await _postgres.DisposeAsync().AsTask();

    private async Task<WebOfWorkDbContext> GetDbContextAsync()
    {
        var opts = new DbContextOptionsBuilder<WebOfWorkDbContext>()
            .UseSnakeCaseNamingConvention().UseNpgsql(_postgres.GetConnectionString())
            .Options;
        var db = new WebOfWorkDbContext(opts);
        await db.Database.EnsureCreatedAsync();
        return db;
    }

    private async Task<(User freelancerUser, Contract contract, Payment payment)> CreateSetupDataAsync(
        WebOfWorkDbContext db,
        ContractStatus status,
        DateTime? payoutEligibleAfter,
        PaymentStatus paymentStatus = PaymentStatus.Escrow,
        string? orderId = null,
        bool phoneVerified = false)
    {
        var clientUser = new User { Id = Guid.NewGuid(), Email = $"c-{Guid.NewGuid():N}@test.com", FullName = "C", Role = UserRole.Client };
        var freelancerUser = new User { Id = Guid.NewGuid(), Email = $"f-{Guid.NewGuid():N}@test.com", FullName = "F", Role = UserRole.Freelancer };
        db.Users.AddRange(clientUser, freelancerUser);
        
        var cp = new ClientProfile { Id = Guid.NewGuid(), UserId = clientUser.Id };
        var fp = new FreelancerProfile
        {
            Id = Guid.NewGuid(),
            UserId = freelancerUser.Id,
            Phone = phoneVerified ? "0771234567" : string.Empty,
            IsPhoneVerified = phoneVerified
        };
        db.ClientProfiles.Add(cp);
        db.FreelancerProfiles.Add(fp);
        await db.SaveChangesAsync();

        var category = new Category { Name = $"Cat-{Guid.NewGuid():N}" };
        db.Categories.Add(category);
        await db.SaveChangesAsync();

        var project = new Project { Title = "P", ClientId = cp.Id, Description = "D", CategoryId = category.Id }; 
        db.Projects.Add(project);
        await db.SaveChangesAsync();

        var contract = new Contract { ProjectId = project.Id, ClientId = cp.Id, FreelancerId = fp.Id, TotalAmount = 100, Status = status, PayoutEligibleAfter = payoutEligibleAfter };
        db.Contracts.Add(contract);
        await db.SaveChangesAsync();

        var payment = new Payment
        {
            ContractId = contract.Id,
            Amount = 100,
            PlatformFee = 10,
            PayPalOrderId = orderId ?? $"OID-{Guid.NewGuid():N}",
            Status = paymentStatus
        };
        db.Payments.Add(payment);
        await db.SaveChangesAsync();

        return (freelancerUser, contract, payment);
    }

    private PayoutService CreateService(
        WebOfWorkDbContext db,
        Mock<IPayPalPayoutClient>? mockPayPal = null,
        Mock<INotificationPublisher>? publisher = null)
    {
        mockPayPal ??= new Mock<IPayPalPayoutClient>();
        mockPayPal.Setup(x => x.SendPayoutAsync(It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<string>())).ReturnsAsync(true);
        return new PayoutService(db, mockPayPal.Object, publisher?.Object);
    }

    [Fact] // 13.1
    public async Task GetPendingPayouts_ReturnsOnlyCompletedEligibleEscrowPayments()
    {
        await using var db = await GetDbContextAsync();
        var (_, _, eligible) = await CreateSetupDataAsync(db, ContractStatus.Completed, DateTime.UtcNow.AddDays(-1), orderId: "eligible");
        await CreateSetupDataAsync(db, ContractStatus.Active, DateTime.UtcNow.AddDays(-1), orderId: "active");
        await CreateSetupDataAsync(db, ContractStatus.Completed, DateTime.UtcNow.AddDays(1), orderId: "future");
        await CreateSetupDataAsync(db, ContractStatus.Completed, DateTime.UtcNow.AddDays(-1), PaymentStatus.Released, "released");

        var service = CreateService(db);

        var payouts = await service.GetPendingPayoutsAsync(CancellationToken.None);

        payouts.Should().ContainSingle();
        payouts[0].Id.Should().Be(eligible.Id);
        payouts[0].Amount.Should().Be(90m);
        // The DTO carries the enum, not its string form. Comparing against
        // `.ToString()` was only ever checking a string against a string, and it
        // no longer type-checks now that the field is typed.
        payouts[0].Status.Should().Be(PaymentStatus.Escrow);
    }

    [Fact] // 13.5
    public async Task ReleasePayout_BeforeContractCompletion_ThrowsInvalidOperationException()
    {
        await using var db = await GetDbContextAsync();
        var (_, _, payment) = await CreateSetupDataAsync(db, ContractStatus.Active, DateTime.UtcNow.AddDays(-1));
        var service = CreateService(db);
        var act = async () => await service.ReleasePayoutAsync(new ReleasePayoutRequest { PaymentId = payment.Id }, CancellationToken.None);
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Contract is not completed*");
    }

    [Fact] // 13.5a
    public async Task ReleasePayout_BeforeEligibilityWindow_ThrowsInvalidOperationException()
    {
        await using var db = await GetDbContextAsync();
        var (_, _, payment) = await CreateSetupDataAsync(db, ContractStatus.Completed, DateTime.UtcNow.AddDays(1));
        var service = CreateService(db);
        var act = async () => await service.ReleasePayoutAsync(new ReleasePayoutRequest { PaymentId = payment.Id }, CancellationToken.None);
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Payout is not yet eligible*");
    }

    [Fact] // 13.6
    public async Task ReleasePayout_Duplicate_ThrowsInvalidOperationException()
    {
        await using var db = await GetDbContextAsync();
        var (_, _, payment) = await CreateSetupDataAsync(db, ContractStatus.Completed, DateTime.UtcNow.AddDays(-1), PaymentStatus.Released);
        var service = CreateService(db);
        var act = async () => await service.ReleasePayoutAsync(new ReleasePayoutRequest { PaymentId = payment.Id }, CancellationToken.None);
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*already released*");
    }

    [Fact] // 13.4, 13.12a
    public async Task ReleasePayout_WhenEligible_ReleasesNetAmountAndSendsUsdSms()
    {
        await using var db = await GetDbContextAsync();
        var (freelancerUser, contract, payment) = await CreateSetupDataAsync(
            db,
            ContractStatus.Completed,
            DateTime.UtcNow.AddDays(-1),
            phoneVerified: true);

        // `ReleasePayoutAsync` reads `GlobalSettings.PayPalEmail` before it pays
        // anything, and returns a failure (surfacing as "PayPal payout failed")
        // when it is unset.
        //
        // Update rather than insert: this suite runs against a real Postgres
        // container that already has the singleton settings row, so adding one
        // with the same primary key is a duplicate-key violation.
        var settings = await db.GlobalSettings.FirstOrDefaultAsync(g => g.Id == 1);
        if (settings is null)
        {
            settings = new GlobalSettings { Id = 1 };
            db.GlobalSettings.Add(settings);
        }
        settings.PayPalEmail = "platform@webofwork.test";
        await db.SaveChangesAsync();

        // The service now pays through `SendPayoutBatchAsync`, not
        // `SendPayoutAsync`. Mocking the old single-recipient overload left the
        // real (unmocked) batch call returning false, which is why this failed
        // with "PayPal payout failed" rather than an assertion.
        var mockPayPal = new Mock<IPayPalPayoutClient>();
        mockPayPal
            .Setup(x => x.SendPayoutBatchAsync(
                It.IsAny<IReadOnlyList<(string Email, decimal Amount, string Note)>>(),
                It.IsAny<string>(),
                It.IsAny<string>()))
            .ReturnsAsync((true, "batch-1", (string?)null));
        var mockSms = new Mock<ISmsSender>();
        var publisher = new Mock<INotificationPublisher>();
        var service = CreateService(db, mockPayPal, publisher);

        var result = await service.ReleasePayoutAsync(new ReleasePayoutRequest { PaymentId = payment.Id }, CancellationToken.None);

        result.Should().BeTrue();
        var updatedPayment = await db.Payments.Include(p => p.Contract).SingleAsync(p => p.Id == payment.Id);
        updatedPayment.Status.Should().Be(PaymentStatus.Released);
        updatedPayment.ProcessedAt.Should().NotBeNull();
        updatedPayment.Contract.PaidToFreelancer.Should().BeTrue();

        // The freelancer is paid, and only the freelancer. The platform fee is
        // deliberately not a second batch item: `GlobalSettings.PayPalEmail` is
        // the sending account, and PayPal rejects a self-payment, which would
        // fail the whole batch and with it the freelancer's payout. So this
        // asserts the item count, not just that a call happened.
        mockPayPal.Verify(x => x.SendPayoutBatchAsync(
            It.Is<IReadOnlyList<(string Email, decimal Amount, string Note)>>(items =>
                items.Count == 1 &&
                items[0].Email == freelancerUser.Email &&
                items[0].Amount == 90m),
            It.Is<string>(s => s.Contains($"#{contract.Id}")),
            It.IsAny<string>()), Times.Once);

        // The SMS assertion moved. `PayoutService` used to send one directly
        // through `ISmsSender`, which meant a freelancer with SMS switched off
        // still got payout texts, and it was also the only reason the tailored
        // message appeared -- the strategy was registered under a key the
        // dispatcher never looks up. SMS is now dispatched by
        // `NotificationService` through this publish call, so the publish is what
        // is asserted here, and the actual SMS text is covered by
        // `SmsStrategyFactoryTests` against the dispatcher.
        //
        // `mockSms` is retained deliberately: nothing in this path may reach it,
        // and a test that quietly stopped needing it would not notice.
        mockSms.Verify(x => x.SendSmsAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);

        publisher.Verify(x => x.PublishAsync(
            freelancerUser.Id,
            NotificationType.PayoutReleased,
            It.Is<string>(m => m.Contains($"#{contract.Id}")),
            "Payment",
            payment.Id,
            It.IsAny<CancellationToken>(),
            90m,
            contract.Id), Times.Once);
    }

    // ---------------------------------------------------------------------
    // ReleaseByContractAsync
    //
    // `POST /api/payments/release-payout` is the route the admin UI actually
    // calls, and it had no test coverage at all. That is why its body was able
    // to drift away from `ReleasePayoutAsync` without anything failing: it
    // checked only that the payment was in escrow, skipped the contract
    // completion check and the payout eligibility window, paid the original
    // `FreelancerShare` rather than the refund-adjusted balance, and published
    // no notification.
    //
    // Both entry points now share one private core, so these tests are really
    // asserting that the contract-addressed route is not a second, weaker
    // payout path.
    // ---------------------------------------------------------------------

    private static (Mock<IPayPalPayoutClient> paypal, Mock<INotificationPublisher> publisher)
        CreatePayPalMock()
    {
        var paypal = new Mock<IPayPalPayoutClient>();
        paypal
            .Setup(x => x.SendPayoutBatchAsync(
                It.IsAny<IReadOnlyList<(string Email, decimal Amount, string Note)>>(),
                It.IsAny<string>(),
                It.IsAny<string>()))
            .ReturnsAsync((true, "batch-by-contract", (string?)null));

        return (paypal, new Mock<INotificationPublisher>());
    }

    private static async Task SetPlatformPayPalEmailAsync(WebOfWorkDbContext db)
    {
        var settings = await db.GlobalSettings.FirstOrDefaultAsync(g => g.Id == 1);
        if (settings is null)
        {
            settings = new GlobalSettings { Id = 1 };
            db.GlobalSettings.Add(settings);
        }
        settings.PayPalEmail = "platform@webofwork.test";
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task ReleaseByContract_RefusesWhenTheContractIsNotCompleted()
    {
        await using var db = await GetDbContextAsync();
        var (_, contract, _) = await CreateSetupDataAsync(
            db, ContractStatus.Active, DateTime.UtcNow.AddDays(-1));
        await SetPlatformPayPalEmailAsync(db);

        var (paypal, publisher) = CreatePayPalMock();
        var service = CreateService(db, paypal, publisher);

        var act = async () => await service.ReleaseByContractAsync(contract.Id, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not completed*");
        paypal.Verify(x => x.SendPayoutBatchAsync(
            It.IsAny<IReadOnlyList<(string, decimal, string)>>(),
            It.IsAny<string>(),
            It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ReleaseByContract_RefusesBeforeTheEligibilityWindowOpens()
    {
        await using var db = await GetDbContextAsync();
        var (_, contract, _) = await CreateSetupDataAsync(
            db, ContractStatus.Completed, DateTime.UtcNow.AddDays(3));
        await SetPlatformPayPalEmailAsync(db);

        var (paypal, publisher) = CreatePayPalMock();
        var service = CreateService(db, paypal, publisher);

        var act = async () => await service.ReleaseByContractAsync(contract.Id, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not yet eligible*");
        paypal.Verify(x => x.SendPayoutBatchAsync(
            It.IsAny<IReadOnlyList<(string, decimal, string)>>(),
            It.IsAny<string>(),
            It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ReleaseByContract_RefusesWhenThereIsNoEligibilityWindowAtAll()
    {
        await using var db = await GetDbContextAsync();
        var (_, contract, _) = await CreateSetupDataAsync(
            db, ContractStatus.Completed, payoutEligibleAfter: null);
        await SetPlatformPayPalEmailAsync(db);

        var (paypal, publisher) = CreatePayPalMock();
        var service = CreateService(db, paypal, publisher);

        var act = async () => await service.ReleaseByContractAsync(contract.Id, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not yet eligible*");
    }

    [Fact]
    public async Task ReleaseByContract_PaysTheRefundAdjustedBalanceNotTheOriginalShare()
    {
        await using var db = await GetDbContextAsync();
        var (freelancerUser, contract, payment) = await CreateSetupDataAsync(
            db, ContractStatus.Completed, DateTime.UtcNow.AddDays(-1));
        await SetPlatformPayPalEmailAsync(db);

        // Amount 100, fee 10, so the full share is 90. Half the payment has been
        // refunded, so only 50 is owed and the fee pro-rates to 5: the payout is
        // 45.
        //
        // The old body paid `payment.FreelancerShare` -- 90 -- so the freelancer
        // received the refunded 45 twice over, once from the refund and once
        // from the payout.
        payment.RefundedAmount = 50m;
        await db.SaveChangesAsync();

        var (paypal, publisher) = CreatePayPalMock();
        var service = CreateService(db, paypal, publisher);

        await service.ReleaseByContractAsync(contract.Id, CancellationToken.None);

        paypal.Verify(x => x.SendPayoutBatchAsync(
            It.Is<IReadOnlyList<(string Email, decimal Amount, string Note)>>(items =>
                items.Count == 1 &&
                items[0].Email == freelancerUser.Email &&
                items[0].Amount == 45m),
            It.Is<string>(s => s.Contains($"#{contract.Id}")),
            It.IsAny<string>()), Times.Once);

        var updated = await db.Payments.Include(p => p.Contract).SingleAsync(p => p.Id == payment.Id);
        updated.Status.Should().Be(PaymentStatus.Released);
        updated.Contract.PaidToFreelancer.Should().BeTrue();
    }

    [Fact]
    public async Task ReleaseByContract_NotifiesTheFreelancer()
    {
        await using var db = await GetDbContextAsync();
        var (freelancerUser, contract, payment) = await CreateSetupDataAsync(
            db, ContractStatus.Completed, DateTime.UtcNow.AddDays(-1));
        await SetPlatformPayPalEmailAsync(db);

        var (paypal, publisher) = CreatePayPalMock();
        var service = CreateService(db, paypal, publisher);

        await service.ReleaseByContractAsync(contract.Id, CancellationToken.None);

        // The old body published nothing, so a freelancer paid through the route
        // the admin UI actually uses was never told.
        publisher.Verify(x => x.PublishAsync(
            freelancerUser.Id,
            NotificationType.PayoutReleased,
            It.Is<string>(m => m.Contains($"#{contract.Id}")),
            "Payment",
            payment.Id,
            It.IsAny<CancellationToken>(),
            It.IsAny<decimal>(),
            contract.Id), Times.Once);
    }

    [Fact]
    public async Task ReleaseByContract_RefusesASecondReleaseOfTheSamePayment()
    {
        await using var db = await GetDbContextAsync();
        var (_, contract, payment) = await CreateSetupDataAsync(
            db, ContractStatus.Completed, DateTime.UtcNow.AddDays(-1));
        await SetPlatformPayPalEmailAsync(db);

        var (paypal, publisher) = CreatePayPalMock();
        var service = CreateService(db, paypal, publisher);

        await service.ReleaseByContractAsync(contract.Id, CancellationToken.None);

        var act = async () => await service.ReleaseByContractAsync(contract.Id, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already released*");
    }

    [Fact]
    public async Task ReleaseByContract_AndReleaseByPaymentId_EnforceTheSameGuards()
    {
        await using var db = await GetDbContextAsync();
        var (_, contract, payment) = await CreateSetupDataAsync(
            db, ContractStatus.Active, DateTime.UtcNow.AddDays(-1));
        await SetPlatformPayPalEmailAsync(db);

        var (paypal, publisher) = CreatePayPalMock();
        var service = CreateService(db, paypal, publisher);

        // Both addresses of the same payment must be refused for the same reason.
        // This is the property that was broken: the two methods had drifted, and
        // the admin-facing one was the weaker of the pair.
        var byContract = async () => await service.ReleaseByContractAsync(contract.Id, CancellationToken.None);
        var byPayment = async () => await service.ReleasePayoutAsync(
            new ReleasePayoutRequest { PaymentId = payment.Id }, CancellationToken.None);

        await byContract.Should().ThrowAsync<InvalidOperationException>().WithMessage("*not completed*");
        await byPayment.Should().ThrowAsync<InvalidOperationException>().WithMessage("*not completed*");
    }
}
