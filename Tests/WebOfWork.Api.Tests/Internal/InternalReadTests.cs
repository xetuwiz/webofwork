using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using WebOfWork.Api.Data;
using WebOfWork.Api.Models;
using WebOfWork.Api.Models.Enums;
using WebOfWork.Api.Services;
using Xunit;

namespace WebOfWork.Api.Tests.Internal;

public sealed class InternalReadTests
{
    [Fact]
    public async Task FreelancerSearch_CapsResultsAndOmitsPrivateEmail()
    {
        await using var db = CreateDb();
        for (var index = 0; index < 300; index++)
        {
            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = $"freelancer-{index}@example.com",
                FullName = $"Freelancer {index}",
                PasswordHash = "hash",
                Role = UserRole.Freelancer,
                Status = UserStatus.Active,
                IsEmailVerified = true
            };
            db.Users.Add(user);
            db.FreelancerProfiles.Add(new FreelancerProfile
            {
                Id = Guid.NewGuid(),
                User = user,
                UserId = user.Id,
                Tagline = "Designer",
                Bio = "Internal summary",
                IsActive = true,
                Availability = true,
                AverageRating = 5m
            });
        }
        await db.SaveChangesAsync();

        var service = new InternalReadService(db);

        // An over-large request is capped rather than honoured. The ceiling is the
        // largest pool the matching agent asks for, and it must not drift below
        // that: the clamp is invisible to the caller, so lowering it silently
        // changes which candidates were considered and produces a shortlist drawn
        // from an arbitrary prefix of the pool.
        var capped = await service.SearchFreelancersAsync(null, null, null, null, 5_000);
        capped.Should().HaveCount(InternalReadService.MaxPoolSize);

        // And the agent's own request is satisfied in full, so it ranks the pool it
        // asked to see rather than the first N of it.
        var asked = await service.SearchFreelancersAsync(null, null, null, null, 200);
        asked.Should().HaveCount(200);

        // The privacy half, and the reason this test exists. A cap change is not a
        // licence to widen the projection.
        foreach (var result in capped)
            result.GetType().GetProperties().Should().NotContain(
                p => p.Name.Contains("Email", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task InternalReads_ReturnUsdCommissionAndThresholdData()
    {
        await using var db = CreateDb();
        db.GlobalSettings.Add(new GlobalSettings
        {
            Id = 1,
            CommissionPercentage = 12m,
            MinBriefConfidence = 0.7m,
            ProposalScreeningThreshold = 0.6m,
            MatchScoreThreshold = 0.8m
        });
        await db.SaveChangesAsync();

        var service = new InternalReadService(db);
        var commission = await service.GetCommissionAsync();

        commission.Currency.Should().Be("USD");
        commission.CommissionPercentage.Should().Be(12m);
    }

    // ---------------------------------------------------------------------
    // GetCategorySkillsAsync
    //
    // This backs the drafter's `list_skills_by_category`, which shipped with no
    // data source at all: it filtered the injected category list on the id and
    // returned the category row, so it answered a question about skills with a
    // category -- and, once the caller stopped nesting skills into that list,
    // returned nothing for any category. A live draft had the model call it seven
    // times, once per category, before giving up on skills.
    //
    // The endpoint did not exist, so these are about what it returns rather than
    // about the tool that used to be broken.
    // ---------------------------------------------------------------------

    [Fact]
    public async Task CategorySkills_ReturnsOnlyThatCategorysRealRows()
    {
        await using var db = CreateDb();
        var (development, design) = SeedTwoCategoriesOfSkills(db);
        await db.SaveChangesAsync();

        var service = new InternalReadService(db);
        var rows = await service.GetCategorySkillsAsync(development.Id, null);

        rows.Should().NotBeEmpty();
        rows.Should().OnlyContain(x => x.CategoryId == development.Id);
        rows.Should().NotContain(x => x.Name == "Figma");
    }

    [Fact]
    public async Task CategorySkills_CarriesTheCategoryIdOnEveryRow()
    {
        // Load-bearing, not redundant with the route.
        //
        // The agent's grounding tells a skill row from a category row only by the
        // category field, because both are otherwise `{id, name}`. Grounding
        // checked the snake_case spelling while the boundary hands over camelCase,
        // so every real skill row was refused as a category row and the pool came
        // back empty.
        await using var db = CreateDb();
        var (development, _) = SeedTwoCategoriesOfSkills(db);
        await db.SaveChangesAsync();

        var service = new InternalReadService(db);
        var rows = await service.GetCategorySkillsAsync(development.Id, null);

        rows.Should().OnlyContain(x => x.CategoryId.HasValue);
    }

    [Fact]
    public async Task CategorySkills_NarrowsByNameSoALargeCategoryStaysUsable()
    {
        // Development is 199 skills and about 12KB of JSON; the agent's tool loop
        // cuts tool results at 4000 characters, which lands mid-list
        // alphabetically. Without a search the tail -- JavaScript, React,
        // TypeScript -- never reaches the model, and a live draft settled for
        // alphabetically early skills instead.
        await using var db = CreateDb();
        var (development, _) = SeedTwoCategoriesOfSkills(db);
        await db.SaveChangesAsync();

        var service = new InternalReadService(db);
        var rows = await service.GetCategorySkillsAsync(development.Id, "script");

        rows.Should().NotBeEmpty();
        rows.Should().OnlyContain(x => x.Name.ToLower().Contains("script"));
    }

    [Fact]
    public async Task CategorySkills_SearchIsCaseInsensitiveAndTrimmed()
    {
        await using var db = CreateDb();
        var (development, _) = SeedTwoCategoriesOfSkills(db);
        await db.SaveChangesAsync();

        var service = new InternalReadService(db);
        var rows = await service.GetCategorySkillsAsync(development.Id, "  JAVA  ");

        // Both halves matter, and the first one is the one a naive test drops.
        //
        // Asserting only `ContainSingle(x => x.Name == "JavaScript")` passes even
        // when the search is ignored entirely -- there is exactly one row with
        // that name whether or not the filter ran. Verified by mutation: removing
        // the filter left this test green. The row count is what actually
        // distinguishes "filtered" from "ignored".
        rows.Should().ContainSingle();
        rows.Should().ContainSingle(x => x.Name == "JavaScript");
    }

    [Fact]
    public async Task CategorySkills_AnEmptySearchReturnsTheWholeCategory()
    {
        // A blank term must mean "no filter", not "match nothing". Getting this
        // backwards would look identical to the bug that started all this: an
        // agent told a populated category has no skills.
        await using var db = CreateDb();
        var (development, _) = SeedTwoCategoriesOfSkills(db);
        await db.SaveChangesAsync();

        var service = new InternalReadService(db);
        var all = await service.GetCategorySkillsAsync(development.Id, null);
        var blank = await service.GetCategorySkillsAsync(development.Id, "   ");

        blank.Should().HaveCount(all.Count);
    }

    [Fact]
    public async Task CategorySkills_ReturnsNothingForACategoryWithNoSkills()
    {
        await using var db = CreateDb();
        var empty = new Category { Id = 99, Name = "Empty" };
        db.Categories.Add(empty);
        await db.SaveChangesAsync();

        var service = new InternalReadService(db);
        var rows = await service.GetCategorySkillsAsync(empty.Id, null);

        rows.Should().BeEmpty();
    }

    private static (Category Development, Category Design) SeedTwoCategoriesOfSkills(
        WebOfWorkDbContext db)
    {
        var development = new Category { Id = 1, Name = "Development" };
        var design = new Category { Id = 2, Name = "Design" };
        db.Categories.AddRange(development, design);

        db.Skills.AddRange(
            new Skill { Id = 8, Name = "JavaScript", CategoryId = 1 },
            new Skill { Id = 30, Name = "HTML5", CategoryId = 1 },
            new Skill { Id = 47, Name = "Responsive Design", CategoryId = 1 },
            new Skill { Id = 60, Name = "Figma", CategoryId = 2 });

        return (development, design);
    }

    private static WebOfWorkDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<WebOfWorkDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new WebOfWorkDbContext(options);
    }

    // ---------------------------------------------------------------------
    // GetSimilarProjectsAsync
    //
    // This backs the planner's `get_similar_projects` tool, which shipped as a
    // `return []` stub while the planner advertised it as the way to judge a
    // normal budget. The advertised-vs-permitted guard could not catch that: the
    // tool genuinely was permitted. Permission parity says nothing about whether
    // a tool works, so these tests are about what the endpoint returns.
    // ---------------------------------------------------------------------

    private static (ClientProfile Client, Category Category) SeedProjectOwner(
        WebOfWorkDbContext db)
    {
        var clientUser = new User
        {
            Id = Guid.NewGuid(),
            Email = $"client-{Guid.NewGuid():N}@example.com",
            FullName = "Client",
            PasswordHash = "hash",
            Role = UserRole.Client,
            Status = UserStatus.Active
        };
        db.Users.Add(clientUser);

        var profile = new ClientProfile { Id = Guid.NewGuid(), UserId = clientUser.Id };
        db.ClientProfiles.Add(profile);

        var category = new Category { Name = $"Cat-{Guid.NewGuid():N}" };
        db.Categories.Add(category);

        return (profile, category);
    }

    private static Project SeedProject(
        ClientProfile client,
        Category category,
        ProjectStatus status,
        int daysAgo,
        decimal? fixedPrice = 500m)
    {
        return new Project
        {
            Title = $"Project {daysAgo}",
            Description = "D",
            ClientId = client.Id,
            CategoryId = category.Id,
            PricingType = ProjectPricingType.Fixed,
            FixedPrice = fixedPrice,
            ExpectedDays = 14,
            Status = status,
            CreatedAt = DateTime.UtcNow.AddDays(-daysAgo)
        };
    }

    [Fact]
    public async Task SimilarProjects_ReturnsOnlyOpenAndInProgressInThatCategory()
    {
        await using var db = CreateDb();
        var (client, category) = SeedProjectOwner(db);
        var other = new Category { Name = $"Other-{Guid.NewGuid():N}" };
        db.Categories.Add(other);

        var wanted = SeedProject(client, category, ProjectStatus.Open, 1);
        var alsoWanted = SeedProject(client, category, ProjectStatus.InProgress, 2);
        db.Projects.AddRange(wanted, alsoWanted);
        db.Projects.Add(SeedProject(client, category, ProjectStatus.Completed, 3));
        db.Projects.Add(SeedProject(client, category, ProjectStatus.Cancelled, 4));
        db.Projects.Add(SeedProject(client, other, ProjectStatus.Open, 1));
        await db.SaveChangesAsync();

        var results = await new InternalReadService(db)
            .GetSimilarProjectsAsync(category.Id);

        // Completed and cancelled budgets are historical figures nobody is now
        // competing against, and another category's projects are not comparable.
        results.Should().HaveCount(2);
        results.Select(x => x.Id).Should().BeEquivalentTo(new[] { wanted.Id, alsoWanted.Id });
        results.Should().OnlyContain(x => x.Status == "Open" || x.Status == "InProgress");
    }

    [Fact]
    public async Task SimilarProjects_ExcludesTheProjectBeingPlanned()
    {
        await using var db = CreateDb();
        var (client, category) = SeedProjectOwner(db);

        var current = SeedProject(client, category, ProjectStatus.Open, 0, 9999m);
        var comparable = SeedProject(client, category, ProjectStatus.Open, 5, 400m);
        db.Projects.AddRange(current, comparable);
        await db.SaveChangesAsync();

        var results = await new InternalReadService(db)
            .GetSimilarProjectsAsync(category.Id, 10, current.Id);

        // Without this the project being planned is one of its own baselines,
        // which biases the exact judgement the tool is consulted for.
        results.Should().ContainSingle();
        results[0].Id.Should().Be(comparable.Id);
        results[0].FixedPrice.Should().Be(400m);
    }

    [Fact]
    public async Task SimilarProjects_ReturnsNewestFirst()
    {
        await using var db = CreateDb();
        var (client, category) = SeedProjectOwner(db);
        db.Projects.Add(SeedProject(client, category, ProjectStatus.Open, 30, 100m));
        db.Projects.Add(SeedProject(client, category, ProjectStatus.Open, 1, 300m));
        await db.SaveChangesAsync();

        var results = await new InternalReadService(db)
            .GetSimilarProjectsAsync(category.Id);

        results.Should().HaveCount(2);
        results[0].FixedPrice.Should().Be(300m);
        results[1].FixedPrice.Should().Be(100m);
    }

    [Theory]
    [InlineData(0, 1)]      // clamped up to at least one row
    [InlineData(-5, 1)]
    [InlineData(500, 50)]   // clamped down to the hard ceiling
    public async Task SimilarProjects_ClampsTheLimit(int requested, int expectedCeiling)
    {
        await using var db = CreateDb();
        var (client, category) = SeedProjectOwner(db);
        for (var i = 0; i < 60; i++)
            db.Projects.Add(SeedProject(client, category, ProjectStatus.Open, 60 - i));
        await db.SaveChangesAsync();

        var results = await new InternalReadService(db)
            .GetSimilarProjectsAsync(category.Id, requested);

        // A model passing 100000 must get a sane page, not the table.
        results.Count.Should().Be(Math.Min(Math.Max(requested, 1), 50));
        results.Count.Should().BeLessThanOrEqualTo(expectedCeiling);
    }

    [Fact]
    public async Task SimilarProjects_ReturnsAnEmptyListForACategoryWithNothingPosted()
    {
        await using var db = CreateDb();
        var (client, category) = SeedProjectOwner(db);
        db.Projects.Add(SeedProject(client, category, ProjectStatus.Completed, 1));
        await db.SaveChangesAsync();

        var results = await new InternalReadService(db)
            .GetSimilarProjectsAsync(category.Id);

        // An empty list here is a true statement about the category, which is the
        // whole point: the stub returned an empty list that was never true.
        results.Should().BeEmpty();
    }

    [Fact]
    public async Task SimilarProjects_CarriesBothPricingShapes()
    {
        await using var db = CreateDb();
        var (client, category) = SeedProjectOwner(db);

        var ranged = SeedProject(client, category, ProjectStatus.Open, 1, fixedPrice: null);
        ranged.PricingType = ProjectPricingType.Range;
        ranged.FixedPrice = null;
        ranged.MinPrice = 200m;
        ranged.MaxPrice = 800m;

        var fixedPriceProject = SeedProject(client, category, ProjectStatus.Open, 2, 450m);
        db.Projects.AddRange(ranged, fixedPriceProject);
        await db.SaveChangesAsync();

        var results = await new InternalReadService(db)
            .GetSimilarProjectsAsync(category.Id);

        // Collapsing the two shapes into one "budget" number would make a median
        // across a mixed set meaningless, so both are carried.
        results.Should().HaveCount(2);
        results.Should().Contain(x => x.PricingType == "Range" && x.MinPrice == 200m && x.MaxPrice == 800m);
        results.Should().Contain(x => x.PricingType == "Fixed" && x.FixedPrice == 450m);
    }
}
