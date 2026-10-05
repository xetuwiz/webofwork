using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using WebOfWork.Api.Configuration;
using WebOfWork.Api.Data;
using WebOfWork.Api.DTOs.Payments;
using WebOfWork.Api.Integrations.PayPal;
using WebOfWork.Api.Integrations.Sms;
using WebOfWork.Api.Models;
using WebOfWork.Api.Models.Enums;
using WebOfWork.Api.Services;
using Xunit;
using Moq;
using Testcontainers.PostgreSql;

namespace WebOfWork.Api.Tests.Payments;

[Trait("Category", "Container")]
public class PaymentTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:15-alpine")
        .Build();

    public async Task InitializeAsync() => await _postgres.StartAsync();
    public async Task DisposeAsync() => await _postgres.DisposeAsync().AsTask();

    private async Task<WebOfWorkDbContext> GetDbContextAsync()
    {
        var opts = new DbContextOptionsBuilder<WebOfWorkDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .UseSnakeCaseNamingConvention()
            .Options;
        var db = new WebOfWorkDbContext(opts);
        await db.Database.MigrateAsync();
        return db;
    }

    // ── Helper: build a PaymentService backed by a Mock IPayPalOrderVerifier ────
    private PaymentService CreateService(WebOfWorkDbContext db, IPayPalOrderVerifier? verifier = null)
    {
        // If no verifier supplied, use a mock PayPalClient that accepts MOCK_<amount>_<currency> order IDs
        verifier ??= new MockPayPalOrderVerifier();
        var commissionCalc = new CommissionCalculator(db);
        // No `ISmsSender` any more. `PaymentService` used to take one to send a
        // direct "payment verified" SMS alongside the notification, which both
        // bypassed the user's notification preferences and duplicated it.
        // Delivery now goes through `INotificationPublisher` alone, so the
        // preference switch actually governs it.
        return new PaymentService(db, verifier, commissionCalc);
    }

    private async Task<(User Client, User Freelancer, Contract Contract)> CreateSetupDataAsync(WebOfWorkDbContext db)
    {
        var clientUser = new User { Id = Guid.NewGuid(), Email = "client@test.com", FullName = "Client", PasswordHash = "hash", Role = UserRole.Client };
        var freelancerUser = new User { Id = Guid.NewGuid(), Email = "freelancer@test.com", FullName = "Freelancer", PasswordHash = "hash", Role = UserRole.Freelancer };
        db.Users.AddRange(clientUser, freelancerUser);

        var clientProfile = new ClientProfile { Id = Guid.NewGuid(), UserId = clientUser.Id };
        var freelancerProfile = new FreelancerProfile { Id = Guid.NewGuid(), UserId = freelancerUser.Id, Phone = "0771234567", IsPhoneVerified = true };
        db.ClientProfiles.Add(clientProfile);
        db.FreelancerProfiles.Add(freelancerProfile);

        db.GlobalSettings.Add(new GlobalSettings { CommissionPercentage = 10m });

        var category = new Category { Name = "Test Category" };
        db.Categories.Add(category);
        await db.SaveChangesAsync();

        var project = new Project { Title = "Test Project", ClientId = clientProfile.Id, Description = "Test", CategoryId = category.Id };
        db.Projects.Add(project);
        await db.SaveChangesAsync();

        var contract = new Contract
        {
            ProjectId = project.Id,
            ClientId = clientProfile.Id,
            FreelancerId = freelancerProfile.Id,
            TotalAmount = 100m,
            Status = ContractStatus.Offered
        };
        db.Contracts.Add(contract);
        await db.SaveChangesAsync();
        return (clientUser, freelancerUser, contract);
    }

    /// <summary>
    /// Verifying a payment must tell the freelancer, and must say something true.
    /// </summary>
    /// <remarks>
    /// Three defects lived in this one path, and all three are invisible without
    /// this test:
    ///
    ///  - a direct SMS was sent alongside the notification, so a freelancer with
    ///    SMS switched off still received it;
    ///  - `Program.cs` registered `PaymentService` twice, the second passing
    ///    `null` for the publisher, so in the running app the notification was
    ///    never sent at all and the bypassing SMS was the only delivery;
    ///  - and both messages said the money had been "credited to your account",
    ///    which is false here -- the payment has just moved to `Escrow`, and the
    ///    freelancer's share is not released until `PayoutService` does it.
    /// </remarks>
    [Fact]
    public async Task VerifyPayment_NotifiesTheFreelancerWithoutClaimingTheMoneyHasArrived()
    {
        await using var db = await GetDbContextAsync();
        var (clientUser, freelancerUser, contract) = await CreateSetupDataAsync(db);

        var publisher = new Mock<INotificationPublisher>();
        publisher
            .Setup(p => p.PublishAsync(
                It.IsAny<Guid>(), It.IsAny<NotificationType>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<CancellationToken>(),
                It.IsAny<object[]>()))
            .ReturnsAsync(new Notification());

        var service = new PaymentService(
            db, new MockPayPalOrderVerifier(), new CommissionCalculator(db), publisher.Object);

        await service.VerifyPaymentAsync(
            clientUser.Id,
            new VerifyPaymentRequest
            {
                ContractId = contract.Id,
                PayPalOrderId = "MOCK_100_USD",
                PayPalAmountUsd = 100m
            },
            CancellationToken.None);

        publisher.Verify(
            p => p.PublishAsync(
                freelancerUser.Id,
                NotificationType.PaymentCredited,
                It.IsAny<string>(),
                "Payment",
                It.IsAny<int?>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<object[]>()),
            Times.Once,
            "the freelancer must be told the client paid, through the path that "
            + "honours their notification preferences");

        var message = (string)publisher.Invocations
            .SelectMany(i => i.Arguments)
            .First(a => a is string s && s.Contains('$'));

        // 10% commission on 100 USD leaves 90.00 for the freelancer.
        message.Should().Contain("90.00");

        // The claim that matters. At this point the payment is in escrow, and
        // saying otherwise tells a freelancer their money has arrived when their
        // balance says otherwise.
        message.ToLowerInvariant().Should().NotContain("credited to your account");
        message.ToLowerInvariant().Should().Contain("escrow");
    }

    // ── 12.10b: proposal auto-accept + others auto-rejected ─────────────────
    [Fact]
    public async Task VerifyPayment_AutoAcceptsWinningProposalAndRejectsOthers()
    {
        await using var db = await GetDbContextAsync();
        var (client, freelancer, contract) = await CreateSetupDataAsync(db);

        // Add two pending proposals — one for the contract freelancer, one for another
        var otherUser = new User { Id = Guid.NewGuid(), Email = "other@test.com", FullName = "Other", PasswordHash = "hash", Role = UserRole.Freelancer };
        db.Users.Add(otherUser);
        var otherFreelancer = new FreelancerProfile { Id = Guid.NewGuid(), UserId = otherUser.Id };
        db.FreelancerProfiles.Add(otherFreelancer);

        var winningPackage = new FreelancerPackage { Id = Guid.NewGuid(), UserId = freelancer.Id, PackageType = "starter", Price = 100m, DeliveryDays = 1, Description = "P1" };
        var losingPackage = new FreelancerPackage { Id = Guid.NewGuid(), UserId = otherUser.Id, PackageType = "starter", Price = 80m, DeliveryDays = 1, Description = "P2" };
        db.FreelancerPackages.AddRange(winningPackage, losingPackage);

        var winningProposal = new Proposal { ProjectId = contract.ProjectId, FreelancerId = contract.FreelancerId, PackageId = winningPackage.Id, BidAmount = 100m, Status = ProposalStatus.Pending };
        var losingProposal = new Proposal { ProjectId = contract.ProjectId, FreelancerId = otherFreelancer.Id, PackageId = losingPackage.Id, BidAmount = 80m, Status = ProposalStatus.Pending };
        db.Proposals.AddRange(winningProposal, losingProposal);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var request = new VerifyPaymentRequest { ContractId = contract.Id, PayPalOrderId = "MOCK_100_USD", PayPalAmountUsd = 100m };
        await service.VerifyPaymentAsync(client.Id, request, CancellationToken.None);

        var winning = await db.Proposals.FindAsync(winningProposal.Id);
        var losing = await db.Proposals.FindAsync(losingProposal.Id);
        winning!.Status.Should().Be(ProposalStatus.Accepted);
        losing!.Status.Should().Be(ProposalStatus.Rejected);
    }

    // ── 12.11: only authenticated client (role enforced at controller level) ──
    [Fact]
    public async Task VerifyPayment_NonClientUser_ThrowsUnauthorizedAccessException()
    {
        await using var db = await GetDbContextAsync();
        var (_, _, contract) = await CreateSetupDataAsync(db);

        // Use a random Guid that has no ClientProfile — simulates non-client/unauthenticated call at service level
        var nonClientUserId = Guid.NewGuid();
        var service = CreateService(db);
        var request = new VerifyPaymentRequest { ContractId = contract.Id, PayPalOrderId = "MOCK_100_USD", PayPalAmountUsd = 100m };

        var act = async () => await service.VerifyPaymentAsync(nonClientUserId, request, CancellationToken.None);
        await act.Should().ThrowAsync<UnauthorizedAccessException>().WithMessage("*Only clients can verify*");
    }

    // ── 12.12: project ownership validation ─────────────────────────────────
    [Fact]
    public async Task VerifyPayment_WrongClient_ThrowsUnauthorizedAccessException()
    {
        await using var db = await GetDbContextAsync();
        var (_, _, contract) = await CreateSetupDataAsync(db);

        var wrongClient = new User { Id = Guid.NewGuid(), Email = "wrong@test.com", FullName = "Wrong", PasswordHash = "hash", Role = UserRole.Client };
        db.Users.Add(wrongClient);
        db.ClientProfiles.Add(new ClientProfile { Id = Guid.NewGuid(), UserId = wrongClient.Id });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var request = new VerifyPaymentRequest { ContractId = contract.Id, PayPalOrderId = "MOCK_100_USD", PayPalAmountUsd = 100m };

        var act = async () => await service.VerifyPaymentAsync(wrongClient.Id, request, CancellationToken.None);
        await act.Should().ThrowAsync<UnauthorizedAccessException>().WithMessage("*do not own this project*");
    }

    // ── 12.13: PayPal amount validation ──────────────────────────────────────
    [Fact]
    public async Task VerifyPayment_WrongAmount_ThrowsArgumentException()
    {
        await using var db = await GetDbContextAsync();
        var (client, _, contract) = await CreateSetupDataAsync(db);
        var service = CreateService(db);

        // MOCK_50_USD order means PayPal collected 50 USD, but frontend said PayPalAmountUsd = 100m.
        // The mock verifier checks if the order amount matches expectedAmountUsd. It won't match (50 != 100).
        var request = new VerifyPaymentRequest { ContractId = contract.Id, PayPalOrderId = "MOCK_50_USD", PayPalAmountUsd = 100m };
        var act = async () => await service.VerifyPaymentAsync(client.Id, request, CancellationToken.None);
        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*Amount or currency mismatch*");
    }

    // ── 12.13a: PayPal currency validation ───────────────────────────────────
    [Fact]
    public async Task VerifyPayment_WrongCurrency_ThrowsArgumentException()
    {
        await using var db = await GetDbContextAsync();
        var (client, _, contract) = await CreateSetupDataAsync(db);
        var service = CreateService(db);

        // MOCK_100_EUR = wrong currency (config is USD)
        var request = new VerifyPaymentRequest { ContractId = contract.Id, PayPalOrderId = "MOCK_100_EUR", PayPalAmountUsd = 100m };
        var act = async () => await service.VerifyPaymentAsync(client.Id, request, CancellationToken.None);
        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*Amount or currency mismatch*");
    }

    // ── 12.14: duplicate order ID rejection ──────────────────────────────────
    [Fact]
    public async Task VerifyPayment_DuplicateOrderId_ThrowsArgumentException()
    {
        await using var db = await GetDbContextAsync();
        var (client, _, contract) = await CreateSetupDataAsync(db);
        var service = CreateService(db);

        var request = new VerifyPaymentRequest { ContractId = contract.Id, PayPalOrderId = "MOCK_100_USD", PayPalAmountUsd = 100m };
        await service.VerifyPaymentAsync(client.Id, request, CancellationToken.None);

        var act = async () => await service.VerifyPaymentAsync(client.Id, request, CancellationToken.None);
        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*already processed*");
    }

    // ── 12.15a: platform fee ledger persisted atomically ─────────────────────
    [Fact]
    public async Task VerifyPayment_PersistsPlatformFeeInAnalytics()
    {
        await using var db = await GetDbContextAsync();
        var (client, _, contract) = await CreateSetupDataAsync(db);
        var service = CreateService(db);

        var request = new VerifyPaymentRequest { ContractId = contract.Id, PayPalOrderId = "MOCK_100_USD", PayPalAmountUsd = 100m };
        await service.VerifyPaymentAsync(client.Id, request, CancellationToken.None);

        var analytics = await db.PlatformAnalytics.SingleAsync();
        analytics.TotalVolumeUsd.Should().Be(100m);
        analytics.TotalProfitUsd.Should().Be(10m);
    }

    // ── 12.15: atomic rollback on failure ────────────────────────────────────
    [Fact]
    public async Task VerifyPayment_Fails_RollsBackTransaction()
    {
        await using var db = await GetDbContextAsync();
        var (client, _, contract) = await CreateSetupDataAsync(db);

        await using var failingDb = await GetDbContextAsync();
        var mockVerifier = new Mock<IPayPalOrderVerifier>();
        mockVerifier.Setup(x => x.VerifyOrderAsync(It.IsAny<string>(), It.IsAny<decimal>())).ReturnsAsync(true);
        var failingCalc = new Mock<ICommissionCalculator>();
        failingCalc.Setup(x => x.CalculateFeeAsync(It.IsAny<decimal>())).ThrowsAsync(new Exception("Simulated failure"));

        var service = new PaymentService(failingDb, mockVerifier.Object, failingCalc.Object);

        var request = new VerifyPaymentRequest { ContractId = contract.Id, PayPalOrderId = "MOCK_100_USD", PayPalAmountUsd = 100m };
        var act = async () => await service.VerifyPaymentAsync(client.Id, request, CancellationToken.None);
        await act.Should().ThrowAsync<Exception>().WithMessage("Simulated failure");

        await using var checkDb = await GetDbContextAsync();
        (await checkDb.Payments.AnyAsync()).Should().BeFalse("Transaction should have rolled back");
        (await checkDb.Contracts.FindAsync(contract.Id))!.IsFunded.Should().BeFalse();
    }
}

// ── In-test mock: parses MOCK_<amount>_<currency> order IDs ─────────────────
file sealed class MockPayPalOrderVerifier : IPayPalOrderVerifier
{
    public Task<bool> VerifyOrderAsync(string orderId, decimal expectedAmountUsd)
    {
        // Format: MOCK_<amount>_<currency>  e.g. MOCK_100_USD
        var parts = orderId.Split('_');
        if (parts.Length != 3 || parts[0] != "MOCK") return Task.FromResult(false);
        if (!decimal.TryParse(parts[1], out var amount)) return Task.FromResult(false);
        var currency = parts[2];
        var valid = currency == "USD" && amount == expectedAmountUsd;
        return Task.FromResult(valid);
    }
}
