using System;
using System.Threading.Tasks;
using Xunit;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using WebOfWork.Api.Data;
using WebOfWork.Api.DTOs.Reviews;
using WebOfWork.Api.Models;
using WebOfWork.Api.Models.Enums;
using WebOfWork.Api.Services;
using WebOfWork.Api.Controllers;
using Moq;

namespace WebOfWork.Api.Tests.Reviews;

public class ReviewTests : IAsyncLifetime
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

    private async Task<(User Client, User Freelancer, Contract Contract)> CreateSetupDataAsync(WebOfWorkDbContext db, ContractStatus status = ContractStatus.Completed)
    {
        var clientUser = new User { Id = Guid.NewGuid(), Email = $"client_{Guid.NewGuid()}@test.com", FullName = "Client" };
        var freelancerUser = new User { Id = Guid.NewGuid(), Email = $"freelancer_{Guid.NewGuid()}@test.com", FullName = "Freelancer" };

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
            TotalAmount = 3000m,
            Status = status
        };

        db.Users.AddRange(clientUser, freelancerUser);
        db.ClientProfiles.Add(clientProfile);
        db.FreelancerProfiles.Add(freelancerProfile);
        db.Projects.Add(project);
        db.Contracts.Add(contract);
        await db.SaveChangesAsync();

        return (clientUser, freelancerUser, contract);
    }

    private ReviewService CreateService(WebOfWorkDbContext db, Guid userId, string role)
    {
        var mockUser = new Mock<ICurrentUser>();
        mockUser.Setup(u => u.Id).Returns(userId);
        mockUser.Setup(u => u.Role).Returns(role);
        return new ReviewService(db, mockUser.Object);
    }

    // Step 11.20: Prevent duplicate reviews for same contract
    [Fact]
    public async Task CreateReview_Duplicate_ThrowsArgumentException()
    {
        await using var db = await GetDbContextAsync();
        var (client, _, contract) = await CreateSetupDataAsync(db);

        var service = CreateService(db, client.Id, "Client");

        var request = new CreateReviewRequest
        {
            ContractId = contract.Id,
            Rating = 4,
            Comment = "Great work!"
        };

        await service.CreateReviewAsync(request, default);

        var act = async () => await service.CreateReviewAsync(request, default);
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*already submitted*");
    }

    // Step 11.21: Contract must be completed
    [Fact]
    public async Task CreateReview_ActiveContract_ThrowsArgumentException()
    {
        await using var db = await GetDbContextAsync();
        var (client, _, contract) = await CreateSetupDataAsync(db, ContractStatus.Active);

        var service = CreateService(db, client.Id, "Client");

        var act = async () => await service.CreateReviewAsync(new CreateReviewRequest
        {
            ContractId = contract.Id,
            Rating = 5,
            Comment = "Nice."
        }, default);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*completed*");
    }

    // Step 11.22: Rating must be 1–5 (validated at DTO/validator level)
    [Fact]
    public async Task CreateReview_ValidRating_Succeeds()
    {
        await using var db = await GetDbContextAsync();
        var (client, freelancer, contract) = await CreateSetupDataAsync(db);

        var service = CreateService(db, client.Id, "Client");
        var result = await service.CreateReviewAsync(new CreateReviewRequest
        {
            ContractId = contract.Id,
            Rating = 5,
            Comment = "Excellent work delivered on time."
        }, default);

        result.Should().NotBeNull();
        result.Rating.Should().Be(5);
        result.Type.Should().Be(ReviewType.Freelancer);

        // Verify rating recalculation updated FreelancerProfile
        var fpProfile = await db.FreelancerProfiles.FirstAsync(p => p.UserId == freelancer.Id);
        fpProfile.AverageRating.Should().Be(5.00m);
        fpProfile.ReviewCount.Should().Be(1);
    }

    // Step 11.23: Self-review prevention
    [Fact]
    public async Task CreateReview_WrongContract_ThrowsUnauthorized()
    {
        await using var db = await GetDbContextAsync();
        var (client, _, contract) = await CreateSetupDataAsync(db);

        // Freelancer trying to review as if they were the client
        var service = CreateService(db, client.Id, "Freelancer");

        var act = async () => await service.CreateReviewAsync(new CreateReviewRequest
        {
            ContractId = contract.Id,
            Rating = 3,
            Comment = "Test"
        }, default);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    // Step 11.28b: Admin delete — non-admin controller returns ForbidResult
    [Fact]
    public async Task DeleteReview_NonAdmin_ReturnsForbid()
    {
        var mockService = new Mock<IReviewService>();
        // The controller now takes its validator explicitly rather than resolving
        // it from DI, so the test supplies one. It is unused here: this test only
        // reflects over the `[Authorize]` attribute.
        var controller = new ReviewsController(
            mockService.Object, new WebOfWork.Api.Validators.Reviews.CreateReviewValidator());

        var methodInfo = typeof(ReviewsController).GetMethod(nameof(ReviewsController.Delete));
        var authAttribute = methodInfo?.GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), true)
            .Cast<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>()
            .FirstOrDefault();

        authAttribute.Should().NotBeNull();
        authAttribute!.Policy.Should().Be("RequireAdmin");
    }

    [Fact]
    public async Task CreateReview_InvalidRating_ThrowsValidationException()
    {
        var request = new CreateReviewRequest
        {
            ContractId = 1,
            Rating = 6, // Out of bounds
            Comment = "Excellent!"
        };

        var validator = new WebOfWork.Api.Validators.Reviews.CreateReviewValidator();
        var result = validator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Rating");
    }

    [Fact]
    public async Task CreateReview_SelfReview_ThrowsArgumentException()
    {
        await using var db = await GetDbContextAsync();
        var (client, freelancer, contract) = await CreateSetupDataAsync(db);

        // A weird edge case where the client somehow ends up as both roles, or tries to review themselves.
        // We set the ClientProfile's UserId to match the Freelancer's UserId.
        var clientProfile = await db.ClientProfiles.FindAsync(contract.ClientId);
        clientProfile!.UserId = freelancer.Id;
        await db.SaveChangesAsync();

        var service = CreateService(db, freelancer.Id, "Client");

        var act = async () => await service.CreateReviewAsync(new CreateReviewRequest
        {
            ContractId = contract.Id,
            Rating = 5,
            Comment = "Great job me!"
        }, default);

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*cannot review yourself*");
    }
    [Fact]
    public async Task CreateReview_AsFreelancer_CreatesClientReview()
    {
        await using var db = await GetDbContextAsync();
        var (client, freelancer, contract) = await CreateSetupDataAsync(db);

        var service = CreateService(db, freelancer.Id, "Freelancer");
        var result = await service.CreateReviewAsync(new CreateReviewRequest
        {
            ContractId = contract.Id,
            Rating = 3,
            Comment = "Client was communicative but slow to respond."
        }, default);

        result.Type.Should().Be(ReviewType.Client);
        result.RevieweeId.Should().Be(client.Id);

        var cpProfile = await db.ClientProfiles.FirstAsync(p => p.UserId == client.Id);
        cpProfile.AverageRating.Should().Be(3.00m);
        cpProfile.ReviewCount.Should().Be(1);
    }
}


