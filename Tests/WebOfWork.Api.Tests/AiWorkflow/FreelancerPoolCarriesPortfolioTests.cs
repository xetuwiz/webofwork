using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using WebOfWork.Api.Data;
using WebOfWork.Api.Models;
using WebOfWork.Api.Models.Enums;
using WebOfWork.Api.Services;
using Xunit;

namespace WebOfWork.Api.Tests.AiWorkflow;

/// <summary>
/// Does the pool the matching agent scores actually contain what it scores?
/// </summary>
/// <remarks>
/// <para>
/// <c>portfolio_relevance</c> is one of the five weights, it is shown to the client
/// as a factor, and the client's weight panel lets a person drag a slider for it.
/// It is measured as term overlap between the brief and the candidate's portfolio
/// text.
/// </para>
/// <para>
/// <c>InternalFreelancerListDto</c> had no <c>Portfolio</c>, so the agent's
/// <c>candidate.get("portfolio")</c> was always <c>None</c> and the weight fell
/// back to packages alone. A freelancer whose portfolio matched the brief exactly
/// scored as though they had none, and the number the client saw was wrong.
/// </para>
/// <para>
/// The field was omitted because the <em>detail</em> endpoint returns portfolio
/// data and the list endpoint is meant to be cheap. That reasoning fails for this
/// caller specifically: the matching stage reads one list and scores everyone in
/// it, and never calls the detail endpoint.
/// </para>
/// </remarks>
public sealed class FreelancerPoolCarriesPortfolioTests
{
    [Fact]
    public async Task The_search_pool_includes_each_candidates_portfolio()
    {
        await using var db = new WebOfWorkDbContext(
            new DbContextOptionsBuilder<WebOfWorkDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);

        var user = new User
        {
            Email = $"{Guid.NewGuid()}@example.com",
            FullName = "Portfolio Owner",
            PasswordHash = "hash",
            Role = UserRole.Freelancer,
            Status = UserStatus.Active,
            IsEmailVerified = true,
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        db.FreelancerProfiles.Add(new FreelancerProfile
        {
            UserId = user.Id,
            Tagline = "Full-stack developer",
            IsActive = true,
            Availability = true,
        });
        await db.SaveChangesAsync();

        db.FreelancerPortfolios.Add(new FreelancerPortfolio
        {
            UserId = user.Id,
            Title = "React storefront",
            Description = "Built a responsive React storefront with Stripe checkout.",
        });
        await db.SaveChangesAsync();

        var service = new InternalReadService(db);
        var pool = await service.SearchFreelancersAsync(query: null, categoryId: null,
            minRating: null, available: null, limit: 50);

        var candidate = pool.Should().ContainSingle().Subject;

        candidate.Portfolio.Should().ContainSingle();
        candidate.Portfolio[0].Title.Should().Be("React storefront");

        // The text the weight is actually measured against. If only the title
        // travelled, the overlap would be measured against a few words and the
        // score would be near-meaningless.
        candidate.Portfolio[0].Description.Should().Contain("Stripe checkout");
    }

    [Fact]
    public async Task A_candidate_with_no_portfolio_yields_an_empty_list_not_a_null()
    {
        // `null` and `[]` behave the same to the agent's `or []`, but a null here
        // would serialise as an absent key after normalisation drops nulls, which
        // is one step further from "no portfolio" than it looks.
        await using var db = new WebOfWorkDbContext(
            new DbContextOptionsBuilder<WebOfWorkDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);

        var user = new User
        {
            Email = $"{Guid.NewGuid()}@example.com",
            FullName = "No Portfolio",
            PasswordHash = "hash",
            Role = UserRole.Freelancer,
            Status = UserStatus.Active,
            IsEmailVerified = true,
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        db.FreelancerProfiles.Add(new FreelancerProfile
        {
            UserId = user.Id,
            Tagline = "Developer",
            IsActive = true,
        });
        await db.SaveChangesAsync();

        var service = new InternalReadService(db);
        var pool = await service.SearchFreelancersAsync(query: null, categoryId: null,
            minRating: null, available: null, limit: 50);

        pool.Should().ContainSingle().Which.Portfolio.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public async Task Portfolios_are_not_crossed_between_candidates()
    {
        // The bug this guards is a join without a key: every candidate coming back
        // with everyone else's portfolio would make `portfolio_relevance` identical
        // across the pool and therefore meaningless for ranking.
        await using var db = new WebOfWorkDbContext(
            new DbContextOptionsBuilder<WebOfWorkDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);

        var userA = new User
        {
            Email = $"{Guid.NewGuid()}@example.com",
            FullName = "A",
            PasswordHash = "hash",
            Role = UserRole.Freelancer,
            Status = UserStatus.Active,
            IsEmailVerified = true,
        };
        var userB = new User
        {
            Email = $"{Guid.NewGuid()}@example.com",
            FullName = "B",
            PasswordHash = "hash",
            Role = UserRole.Freelancer,
            Status = UserStatus.Active,
            IsEmailVerified = true,
        };
        db.Users.AddRange(userA, userB);
        await db.SaveChangesAsync();

        db.FreelancerProfiles.AddRange(
            new FreelancerProfile { UserId = userA.Id, Tagline = "A", IsActive = true },
            new FreelancerProfile { UserId = userB.Id, Tagline = "B", IsActive = true });
        await db.SaveChangesAsync();

        db.FreelancerPortfolios.AddRange(
            new FreelancerPortfolio { UserId = userA.Id, Title = "A work", Description = "A" },
            new FreelancerPortfolio { UserId = userB.Id, Title = "B work", Description = "B" },
            new FreelancerPortfolio { UserId = userB.Id, Title = "B work 2", Description = "B2" });
        await db.SaveChangesAsync();

        var service = new InternalReadService(db);
        var pool = await service.SearchFreelancersAsync(query: null, categoryId: null,
            minRating: null, available: null, limit: 50);

        pool.Should().HaveCount(2);

        var a = pool.Single(x => x.FullName == "A");
        var b = pool.Single(x => x.FullName == "B");

        a.Portfolio.Should().ContainSingle().Which.Title.Should().Be("A work");
        b.Portfolio.Should().HaveCount(2);
    }

    [Fact]
    public void The_pool_ceiling_is_not_below_what_the_agent_requests()
    {
        // `Math.Clamp(limit, 1, 50)` while the agent passes 200 meant the pool was
        // silently truncated to its top 50 by rating. A shortlist built from a
        // prefix is a different answer to a different question, and the response
        // gave no indication that anything was dropped.
        InternalReadService.MaxPoolSize.Should().BeGreaterThanOrEqualTo(200);
    }
}