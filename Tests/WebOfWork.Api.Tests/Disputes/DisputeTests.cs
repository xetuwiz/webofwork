using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using WebOfWork.Api.Data;
using WebOfWork.Api.DTOs.Disputes;
using WebOfWork.Api.DTOs.Refunds;
using WebOfWork.Api.Models;
using WebOfWork.Api.Models.Enums;
using WebOfWork.Api.Services;
using Moq;

namespace WebOfWork.Api.Tests.Disputes;

[Trait("Category", "Container")]
public class DisputeTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:15-alpine")
        .WithDatabase("webofwork_test")
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

    private async Task<(User Client, User Freelancer, User Outsider, Contract Contract)> CreateSetupDataAsync(WebOfWorkDbContext db)
    {
        var clientUser = new User { Id = Guid.NewGuid(), Email = $"client_{Guid.NewGuid()}@test.com", FullName = "Client" };
        var freelancerUser = new User { Id = Guid.NewGuid(), Email = $"freelancer_{Guid.NewGuid()}@test.com", FullName = "Freelancer" };
        var outsiderUser = new User { Id = Guid.NewGuid(), Email = $"outsider_{Guid.NewGuid()}@test.com", FullName = "Outsider" };

        var clientProfile = new ClientProfile { Id = Guid.NewGuid(), UserId = clientUser.Id, User = clientUser };
        var freelancerProfile = new FreelancerProfile { Id = Guid.NewGuid(), UserId = freelancerUser.Id, User = freelancerUser };

        var project = new Project { Title = "Test Project", ClientId = clientProfile.Id };

        var contract = new Contract
        {
            Project = project,
            ClientId = clientProfile.Id,
            Client = clientProfile,
            FreelancerId = freelancerProfile.Id,
            Freelancer = freelancerProfile,
            TotalAmount = 5000m,
            Status = ContractStatus.Active
        };

        db.Users.AddRange(clientUser, freelancerUser, outsiderUser);
        db.ClientProfiles.Add(clientProfile);
        db.FreelancerProfiles.Add(freelancerProfile);
        db.Projects.Add(project);
        db.Contracts.Add(contract);
        await db.SaveChangesAsync();

        return (clientUser, freelancerUser, outsiderUser, contract);
    }

    private DisputeService CreateService(WebOfWorkDbContext db, Guid userId, Mock<IRefundService>? mockRefund = null)
    {
        var mockUser = new Mock<ICurrentUser>();
        mockUser.Setup(u => u.Id).Returns(userId);
        mockRefund ??= new Mock<IRefundService>();
        mockRefund
            .Setup(x => x.RefundPaymentAsync(It.IsAny<RefundPaymentRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        return new DisputeService(db, mockUser.Object, mockRefund.Object);
    }

    // Step 11.7: Non-participant gets 403
    [Fact]
    public async Task CreateDispute_AsOutsider_ThrowsUnauthorized()
    {
        await using var db = await GetDbContextAsync();
        var (_, _, outsider, contract) = await CreateSetupDataAsync(db);

        var service = CreateService(db, outsider.Id);

        var act = async () => await service.CreateDisputeAsync(contract.Id, new CreateDisputeRequest
        {
            Reason = DisputeReason.PoorQuality,
            Description = "Work does not match the agreed scope at all.",
            EvidenceUrls = new List<string>()
        }, default);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    // Step 11.8: Duplicate dispute is blocked
    [Fact]
    public async Task CreateDispute_DuplicateOnSameContract_ThrowsArgumentException()
    {
        await using var db = await GetDbContextAsync();
        var (client, _, _, contract) = await CreateSetupDataAsync(db);

        var service = CreateService(db, client.Id);

        var request = new CreateDisputeRequest
        {
            Reason = DisputeReason.MissedDeadline,
            Description = "Deadline was missed by two weeks.",
            EvidenceUrls = new List<string>()
        };

        await service.CreateDisputeAsync(contract.Id, request, default);

        var act = async () => await service.CreateDisputeAsync(contract.Id, request, default);
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*already exists*");
    }

    // Step 11.11b: Participant can view their own dispute; outsider cannot
    [Fact]
    public async Task GetParticipantDispute_AsOutsider_ThrowsUnauthorized()
    {
        await using var db = await GetDbContextAsync();
        var (client, _, outsider, contract) = await CreateSetupDataAsync(db);

        var clientService = CreateService(db, client.Id);
        await clientService.CreateDisputeAsync(contract.Id, new CreateDisputeRequest
        {
            Reason = DisputeReason.IncompleteWork,
            Description = "Work was submitted incomplete.",
            EvidenceUrls = new List<string>()
        }, default);

        var outsiderService = CreateService(db, outsider.Id);
        var act = async () => await outsiderService.GetParticipantDisputeAsync(contract.Id, default);
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    // Step 11.14: Non-admin cannot resolve (tested via controller via Moq)
    [Fact]
    public async Task CreateDispute_AsClient_Succeeds()
    {
        await using var db = await GetDbContextAsync();
        var (client, _, _, contract) = await CreateSetupDataAsync(db);

        var service = CreateService(db, client.Id);
        var result = await service.CreateDisputeAsync(contract.Id, new CreateDisputeRequest
        {
            Reason = DisputeReason.ScopeCreep,
            Description = "Client requesting extra work outside the agreed scope.",
            EvidenceUrls = new List<string> { "https://example.com/evidence.pdf" }
        }, default);

        result.Should().NotBeNull();
        result.Status.Should().Be(DisputeStatus.Pending);
        result.EvidenceUrls.Should().HaveCount(1);

        var dbContract = await db.Contracts.FindAsync(contract.Id);
        dbContract!.Status.Should().Be(ContractStatus.Disputed);
    }

    // Step 11.14a: Refund resolution chains to RefundService
    [Fact]
    public async Task ResolveDispute_WithRefund_CallsRefundService()
    {
        await using var db = await GetDbContextAsync();
        var (client, _, _, contract) = await CreateSetupDataAsync(db);

        var payment = new Payment
        {
            ContractId = contract.Id,
            Amount = contract.TotalAmount,
            PlatformFee = 500m,
            PayPalOrderId = $"OID-{Guid.NewGuid():N}",
            Status = PaymentStatus.Escrow
        };
        db.Payments.Add(payment);
        await db.SaveChangesAsync();

        var service = CreateService(db, client.Id);
        var dispute = await service.CreateDisputeAsync(contract.Id, new CreateDisputeRequest
        {
            Reason = DisputeReason.IncompleteWork,
            Description = "Freelancer submitted incomplete work.",
            EvidenceUrls = new List<string>()
        }, default);

        var mockRefund = new Mock<IRefundService>();
        mockRefund
            .Setup(x => x.RefundPaymentAsync(It.IsAny<RefundPaymentRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var adminUser = new Mock<ICurrentUser>();
        adminUser.Setup(u => u.Id).Returns(Guid.NewGuid());
        var adminService = new DisputeService(db, adminUser.Object, mockRefund.Object);

        var resolved = await adminService.ResolveDisputeAsync(dispute.Id, new ResolveDisputeRequest
        {
            Status = DisputeStatus.Resolved,
            AdminNotes = "Client's claim is valid. Full refund issued.",
            Resolution = DisputeResolution.Refund
        }, default);

        resolved.Status.Should().Be(DisputeStatus.Resolved);
        resolved.Resolution.Should().Be(DisputeResolution.Refund);

        mockRefund.Verify(
            x => x.RefundPaymentAsync(
                It.Is<RefundPaymentRequest>(r =>
                    r.PaymentId == payment.Id &&
                    r.Reason == "Client's claim is valid. Full refund issued."),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // Step 11.31: Dispute status transition to refunded flow
    [Fact]
    public async Task ResolveDispute_ReleaseToFreelancer_SetsContractCompleted()
    {
        await using var db = await GetDbContextAsync();
        var (client, _, _, contract) = await CreateSetupDataAsync(db);

        var service = CreateService(db, client.Id);
        var dispute = await service.CreateDisputeAsync(contract.Id, new CreateDisputeRequest
        {
            Reason = DisputeReason.NoFeedback,
            Description = "Client has been unresponsive for weeks.",
            EvidenceUrls = new List<string>()
        }, default);

        var adminService = CreateService(db, Guid.NewGuid());

        var resolved = await adminService.ResolveDisputeAsync(dispute.Id, new ResolveDisputeRequest
        {
            Status = DisputeStatus.Resolved,
            AdminNotes = "Freelancer submitted quality work. Payment released.",
            Resolution = DisputeResolution.ReleaseToFreelancer
        }, default);

        resolved.Resolution.Should().Be(DisputeResolution.ReleaseToFreelancer);

        var dbContract = await db.Contracts.FindAsync(contract.Id);
        dbContract!.Status.Should().Be(ContractStatus.Completed);
        dbContract.PaidToFreelancer.Should().BeTrue();
    }
}
