using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using WebOfWork.Api.Data;
using WebOfWork.Api.Models;
using WebOfWork.Api.Models.Enums;
using WebOfWork.Api.Services;
using Xunit;

namespace WebOfWork.Api.Tests.Admin;

public sealed class AdminAnalyticsTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:15-alpine")
        .WithDatabase("webofwork_admin_analytics_test")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    public Task InitializeAsync() => _postgres.StartAsync();

    public Task DisposeAsync() => _postgres.DisposeAsync().AsTask();

    [Fact]
    public async Task GetDashboardStatsAsync_CalculatesUsdMetrics()
    {
        await using var db = await CreateDbContextAsync();

        var client = new User { Id = Guid.NewGuid(), Email = "client@test.com", FullName = "Client One", PasswordHash = "hash", Role = UserRole.Client, Status = UserStatus.Active };
        var freelancer = new User { Id = Guid.NewGuid(), Email = "freelancer@test.com", FullName = "Freelancer One", PasswordHash = "hash", Role = UserRole.Freelancer, Status = UserStatus.Active };
        var clientProfile = new ClientProfile { Id = Guid.NewGuid(), User = client, CompanyName = "Client Co", Location = "Colombo" };
        var freelancerProfile = new FreelancerProfile { Id = Guid.NewGuid(), User = freelancer, Tagline = "Tagline", Bio = "Bio", Phone = "0770000001", IsPhoneVerified = true, IsActive = true, Availability = true };

        db.Users.AddRange(client, freelancer);
        db.ClientProfiles.Add(clientProfile);
        db.FreelancerProfiles.Add(freelancerProfile);

        db.Projects.Add(new Project { ClientId = clientProfile.Id, Client = clientProfile, Title = "Project 1", Description = "Desc", Requirements = "Req", ExpectedDeliverables = "Deliv", PricingType = ProjectPricingType.Fixed, FixedPrice = 1000m, ExpectedDays = 10, Status = ProjectStatus.Open, IsActive = true });
        db.Projects.Add(new Project { ClientId = clientProfile.Id, Client = clientProfile, Title = "Project 2", Description = "Desc", Requirements = "Req", ExpectedDeliverables = "Deliv", PricingType = ProjectPricingType.Fixed, FixedPrice = 2000m, ExpectedDays = 10, Status = ProjectStatus.Completed, IsActive = true });

        db.Payments.Add(new Payment { ContractId = 1, PayPalOrderId = "PO1", Amount = 1000m, PlatformFee = 100m, Status = PaymentStatus.Released, ProcessedAt = DateTime.UtcNow });
        db.Payments.Add(new Payment { ContractId = 2, PayPalOrderId = "PO2", Amount = 500m, PlatformFee = 50m, Status = PaymentStatus.Escrow, ProcessedAt = DateTime.UtcNow });
        db.Contracts.Add(new Contract { Id = 1, Project = db.Projects.Local.First(), Client = clientProfile, Freelancer = freelancerProfile, TotalAmount = 1000m, Status = ContractStatus.Completed, PayoutEligibleAfter = DateTime.UtcNow.AddDays(-1) });
        db.Contracts.Add(new Contract { Id = 2, Project = db.Projects.Local.Last(), Client = clientProfile, Freelancer = freelancerProfile, TotalAmount = 500m, Status = ContractStatus.Completed, PayoutEligibleAfter = DateTime.UtcNow.AddDays(-1) });

        await db.SaveChangesAsync();

        var result = await new AdminAnalyticsService(db).GetDashboardStatsAsync(CancellationToken.None);

        result.TotalRevenueUsd.Should().Be(1000m);
        result.TotalPlatformFeesUsd.Should().Be(100m);
        result.NetRevenueUsd.Should().Be(900m);
        result.TotalPendingPayoutsUsd.Should().Be(450m);
        result.PendingPayoutCount.Should().Be(1);
        result.TotalUsers.Should().Be(2);
        result.ActiveUsers.Should().Be(2);
    }

    [Fact]
    public async Task GetRevenueChartAsync_And_GetProposalsChartAsync_ReturnsDailyBuckets()
    {
        await using var db = await CreateDbContextAsync();

        var today = DateTime.UtcNow.Date;
        var clientUser = new User { Id = Guid.NewGuid(), Email = "client@test.com", FullName = "Client One", PasswordHash = "hash", Role = UserRole.Client, Status = UserStatus.Active };
        var freelancerUser = new User { Id = Guid.NewGuid(), Email = "freelancer@test.com", FullName = "Freelancer One", PasswordHash = "hash", Role = UserRole.Freelancer, Status = UserStatus.Active };
        var clientProfile = new ClientProfile { Id = Guid.NewGuid(), User = clientUser, CompanyName = "Client Co", Location = "Colombo" };
        var freelancerProfile = new FreelancerProfile { Id = Guid.NewGuid(), User = freelancerUser, Tagline = "Tagline", Bio = "Bio", Phone = "0770000001", IsPhoneVerified = true, IsActive = true, Availability = true };
        var project = new Project { ClientId = clientProfile.Id, Client = clientProfile, Title = "Project 1", Description = "Desc", Requirements = "Req", ExpectedDeliverables = "Deliv", PricingType = ProjectPricingType.Fixed, FixedPrice = 1000m, ExpectedDays = 10, Status = ProjectStatus.Open, IsActive = true };
        var contract = new Contract { Project = project, Client = clientProfile, Freelancer = freelancerProfile, TotalAmount = 1000m, Status = ContractStatus.Completed, PayoutEligibleAfter = today.AddDays(-1) };
        var firstProposalUser = new User { Id = Guid.NewGuid(), Email = "f1@test.com", FullName = "Freelancer 1", PasswordHash = "hash", Role = UserRole.Freelancer, Status = UserStatus.Active };
        var secondProposalUser = new User { Id = Guid.NewGuid(), Email = "f2@test.com", FullName = "Freelancer 2", PasswordHash = "hash", Role = UserRole.Freelancer, Status = UserStatus.Active };
        var firstProposalFreelancer = new FreelancerProfile { Id = Guid.NewGuid(), User = firstProposalUser, Tagline = "t", Bio = "b", IsActive = true, Availability = true };
        var secondProposalFreelancer = new FreelancerProfile { Id = Guid.NewGuid(), User = secondProposalUser, Tagline = "t", Bio = "b", IsActive = true, Availability = true };

        db.Users.AddRange(clientUser, freelancerUser, firstProposalUser, secondProposalUser);
        db.ClientProfiles.Add(clientProfile);
        db.FreelancerProfiles.AddRange(freelancerProfile, firstProposalFreelancer, secondProposalFreelancer);
        db.Projects.Add(project);
        await db.SaveChangesAsync();

        var savedProjectId = project.Id;
        db.Contracts.Add(contract);
        db.Payments.Add(new Payment { ContractId = contract.Id, Contract = contract, PayPalOrderId = "P1", Amount = 100m, PlatformFee = 10m, Status = PaymentStatus.Released, ProcessedAt = today.AddHours(1) });
        db.Proposals.Add(new Proposal { ProjectId = savedProjectId, FreelancerId = firstProposalFreelancer.Id, Freelancer = firstProposalFreelancer, PackageId = Guid.NewGuid(), Package = new FreelancerPackage { Id = Guid.NewGuid(), UserId = firstProposalUser.Id, PackageType = "starter", Price = 10m, DeliveryDays = 5, Description = "desc" }, CoverLetter = "Hi", BidAmount = 100m, DeliveryDays = 5, Status = ProposalStatus.Accepted, CreatedAt = today });
        db.Proposals.Add(new Proposal { ProjectId = savedProjectId, FreelancerId = secondProposalFreelancer.Id, Freelancer = secondProposalFreelancer, PackageId = Guid.NewGuid(), Package = new FreelancerPackage { Id = Guid.NewGuid(), UserId = secondProposalUser.Id, PackageType = "pro", Price = 20m, DeliveryDays = 6, Description = "desc" }, CoverLetter = "Hi", BidAmount = 200m, DeliveryDays = 6, Status = ProposalStatus.Rejected, CreatedAt = today });
        await db.SaveChangesAsync();

        // Only the revenue chart moved to a date window; the proposals chart still
        // takes a day count. Worth noting as an inconsistency in the service, but
        // not one to change here -- the test should match what exists.
        var revenue = await new AdminAnalyticsService(db)
            .GetRevenueChartAsync(today.AddDays(-7), null, CancellationToken.None);
        var proposals = await new AdminAnalyticsService(db)
            .GetProposalsChartAsync(7, CancellationToken.None);

        revenue.Should().NotBeEmpty();
        proposals.Should().NotBeEmpty();
        proposals.Sum(x => x.Submitted).Should().BeGreaterThanOrEqualTo(2);
    }

    private async Task<WebOfWorkDbContext> CreateDbContextAsync()
    {
        var options = new DbContextOptionsBuilder<WebOfWorkDbContext>()
            .UseSnakeCaseNamingConvention().UseNpgsql(_postgres.GetConnectionString())
            .Options;
        var db = new WebOfWorkDbContext(options);
        await db.Database.EnsureCreatedAsync();
        return db;
    }
}
