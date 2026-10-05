using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Testcontainers.PostgreSql;
using WebOfWork.Api.Data;
using WebOfWork.Api.DTOs.Refunds;
using WebOfWork.Api.Integrations.PayPal;
using WebOfWork.Api.Models;
using WebOfWork.Api.Models.Enums;
using WebOfWork.Api.Services;
using Xunit;

namespace WebOfWork.Api.Tests.Refunds;

[Trait("Category", "Container")]
public class RefundTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:15-alpine")
        .Build();

    public async Task InitializeAsync() => await _postgres.StartAsync();
    public async Task DisposeAsync() => await _postgres.DisposeAsync().AsTask();

    private async Task<WebOfWorkDbContext> GetDbContextAsync()
    {
        var opts = new DbContextOptionsBuilder<WebOfWorkDbContext>()
            .UseSnakeCaseNamingConvention().UseNpgsql(_postgres.GetConnectionString())
            .Options;
        var db = new WebOfWorkDbContext(opts);
        await db.Database.EnsureCreatedAsync();
        return db;
    }

    private async Task<(Contract contract, Payment payment)> CreateEscrowPaymentAsync(
        WebOfWorkDbContext db,
        PaymentStatus status = PaymentStatus.Escrow,
        decimal amount = 100m,
        decimal platformFee = 10m)
    {
        var clientUser = new User
        {
            Id = Guid.NewGuid(),
            Email = $"c-{Guid.NewGuid():N}@test.com",
            FullName = "Client",
            PasswordHash = "hash",
            Role = UserRole.Client
        };
        var freelancerUser = new User
        {
            Id = Guid.NewGuid(),
            Email = $"f-{Guid.NewGuid():N}@test.com",
            FullName = "Freelancer",
            PasswordHash = "hash",
            Role = UserRole.Freelancer
        };
        db.Users.AddRange(clientUser, freelancerUser);

        var clientProfile = new ClientProfile { Id = Guid.NewGuid(), UserId = clientUser.Id };
        var freelancerProfile = new FreelancerProfile { Id = Guid.NewGuid(), UserId = freelancerUser.Id };
        db.ClientProfiles.Add(clientProfile);
        db.FreelancerProfiles.Add(freelancerProfile);

        db.PlatformAnalytics.Add(new PlatformAnalytics
        {
            TotalVolumeUsd = amount,
            TotalProfitUsd = platformFee
        });

        var category = new Category { Name = $"Cat-{Guid.NewGuid():N}" };
        db.Categories.Add(category);
        await db.SaveChangesAsync();

        var project = new Project
        {
            Title = "Project",
            ClientId = clientProfile.Id,
            Description = "Desc",
            CategoryId = category.Id
        };
        db.Projects.Add(project);
        await db.SaveChangesAsync();

        var contract = new Contract
        {
            ProjectId = project.Id,
            ClientId = clientProfile.Id,
            FreelancerId = freelancerProfile.Id,
            TotalAmount = amount,
            Status = ContractStatus.Active,
            IsFunded = true
        };
        db.Contracts.Add(contract);
        await db.SaveChangesAsync();

        var payment = new Payment
        {
            ContractId = contract.Id,
            Amount = amount,
            PlatformFee = platformFee,
            PayPalOrderId = $"OID-{Guid.NewGuid():N}",
            Status = status
        };
        db.Payments.Add(payment);
        await db.SaveChangesAsync();

        return (contract, payment);
    }

    private RefundService CreateService(WebOfWorkDbContext db, Mock<IPayPalRefundClient>? mockPayPal = null)
    {
        mockPayPal ??= new Mock<IPayPalRefundClient>();
        mockPayPal
            .Setup(x => x.RefundOrderAsync(It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<string>()))
            .ReturnsAsync(true);
        return new RefundService(db, mockPayPal.Object);
    }

    [Fact]
    public async Task RefundPayment_WhenEscrow_RefundsViaPayPalAndUpdatesLedger()
    {
        await using var db = await GetDbContextAsync();
        var (contract, payment) = await CreateEscrowPaymentAsync(db);

        var mockPayPal = new Mock<IPayPalRefundClient>();
        mockPayPal
            .Setup(x => x.RefundOrderAsync(payment.PayPalOrderId, payment.Amount, "Client dispute"))
            .ReturnsAsync(true);
        var service = CreateService(db, mockPayPal);

        var result = await service.RefundPaymentAsync(
            new RefundPaymentRequest { PaymentId = payment.Id, Reason = "Client dispute" },
            CancellationToken.None);

        result.Should().BeTrue();

        var updatedPayment = await db.Payments.Include(p => p.Contract).SingleAsync(p => p.Id == payment.Id);
        updatedPayment.Status.Should().Be(PaymentStatus.Refunded);
        updatedPayment.ProcessedAt.Should().NotBeNull();
        updatedPayment.Contract.Status.Should().Be(ContractStatus.Refunded);

        var analytics = await db.PlatformAnalytics.SingleAsync();
        analytics.TotalVolumeUsd.Should().Be(0m);
        analytics.TotalProfitUsd.Should().Be(0m);

        mockPayPal.Verify(
            x => x.RefundOrderAsync(payment.PayPalOrderId, payment.Amount, "Client dispute"),
            Times.Once);
    }

    [Fact]
    public async Task RefundPayment_AlreadyRefunded_ThrowsInvalidOperationException()
    {
        await using var db = await GetDbContextAsync();
        var (_, payment) = await CreateEscrowPaymentAsync(db, PaymentStatus.Refunded);
        var service = CreateService(db);

        var act = async () => await service.RefundPaymentAsync(
            new RefundPaymentRequest { PaymentId = payment.Id, Reason = "Again" },
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*already refunded*");
    }

    [Fact]
    public async Task RefundPayment_NotInEscrow_ThrowsInvalidOperationException()
    {
        await using var db = await GetDbContextAsync();
        var (_, payment) = await CreateEscrowPaymentAsync(db, PaymentStatus.Released);
        var service = CreateService(db);

        var act = async () => await service.RefundPaymentAsync(
            new RefundPaymentRequest { PaymentId = payment.Id, Reason = "Too late" },
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Escrow*");
    }

    [Fact]
    public async Task RefundPayment_PayPalFailure_ThrowsInvalidOperationException()
    {
        await using var db = await GetDbContextAsync();
        var (contract, payment) = await CreateEscrowPaymentAsync(db);

        var mockPayPal = new Mock<IPayPalRefundClient>();
        mockPayPal
            .Setup(x => x.RefundOrderAsync(It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<string>()))
            .ReturnsAsync(false);
        var service = new RefundService(db, mockPayPal.Object);

        var act = async () => await service.RefundPaymentAsync(
            new RefundPaymentRequest { PaymentId = payment.Id, Reason = "Failed gateway" },
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*PayPal refund failed*");

        var unchanged = await db.Payments.Include(p => p.Contract).SingleAsync(p => p.Id == payment.Id);
        unchanged.Status.Should().Be(PaymentStatus.Escrow);
        unchanged.Contract.Status.Should().Be(contract.Status);
        (await db.PlatformAnalytics.SingleAsync()).TotalVolumeUsd.Should().Be(100m);
    }
}
