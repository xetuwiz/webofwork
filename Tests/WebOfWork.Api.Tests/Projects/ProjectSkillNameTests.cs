using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using WebOfWork.Api.Data;
using WebOfWork.Api.DTOs.Projects;
using WebOfWork.Api.Models;
using WebOfWork.Api.Models.Enums;
using WebOfWork.Api.Services;
using Xunit;

namespace WebOfWork.Api.Tests.Projects;

/// <summary>
/// Skill names arrive as free text and are matched against the catalogue.
/// </summary>
/// <remarks>
/// Two defects, both found by running the client schema over all 654 seeded skill
/// names rather than by reading either side.
///
/// <para><b>The names the catalogue actually contains.</b> The client's schema
/// validated each name with <c>/^[a-zA-Z\s.]*$/</c> and <c>/^[^0-9]*$/</c>, which
/// rejected 43 real skills -- C#, C++, HTML5, CSS3, GPT-4, Llama 3, n8n, S3,
/// EC2, Neo4j, .NET. The form's own suggestion chips offered C# and C++ while its
/// own validation refused them, and the drafting agent's suggestion for a
/// responsive-website brief could not be posted at all. Those tests live on the
/// client; this file pins the server half, because the match is
/// case-insensitive on both sides and must stay that way for exactly those
/// names.</para>
///
/// <para><b>Blank input.</b> This loop read <c>raw.Trim()[0]</c> and only then
/// checked <c>name.Length == 0</c>, so the guard could never run: building
/// <c>name</c> from an empty string is the thing that throws. A whitespace-only
/// skill therefore produced an <see cref="IndexOutOfRangeException"/> -- a 500
/// from the code that existed to prevent one. Reachable, because the old client
/// regexes allowed <c>\s</c>, so <c>["  ", "  ", "  "]</c> satisfied the entire
/// field.</para>
/// </remarks>
public sealed class ProjectSkillNameTests
{
    [Theory]
    [InlineData("C#")]
    [InlineData("C++")]
    [InlineData("HTML5")]
    [InlineData("CSS3")]
    [InlineData("GPT-4")]
    [InlineData("Llama 3")]
    [InlineData("n8n")]
    [InlineData("S3")]
    [InlineData(".NET")]
    [InlineData("A/B Testing")]
    public async Task A_CatalogueSkill_IsMatchedToItsRow_Whatever_Its_Casing(string name)
    {
        await using var db = CreateDb();
        var user = SeedClient(db);
        db.Skills.Add(new Skill { Id = 4242, Name = name, CategoryId = null });
        await db.SaveChangesAsync();

        var project = await CreateProject(db, user, name);

        var saved = await db.ProjectSkills
            .Where(ps => ps.ProjectId == project.Id)
            .SingleAsync();

        saved.SkillId.Should().Be(4242, $"'{name}' is a real catalogue row and must link to it");
        saved.CustomName.Should().BeNull("a matched skill is stored by id, not by name");
    }

    [Fact]
    public async Task An_Unknown_Name_Becomes_A_Custom_Skill_Rather_Than_A_Null_Row()
    {
        // The other half of the check constraint: either SkillId or CustomName.
        // A name not in the catalogue must still be recorded, or the client's
        // carefully-typed skill disappears silently.
        await using var db = CreateDb();
        var user = SeedClient(db);

        var project = await CreateProject(db, user, "Underwater Basket Weaving");

        var saved = await db.ProjectSkills
            .Where(ps => ps.ProjectId == project.Id)
            .SingleAsync();

        saved.SkillId.Should().BeNull();
        saved.CustomName.Should().Be("Underwater Basket Weaving");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t")]
    public async Task A_Blank_Skill_Is_Skipped_Rather_Than_Throwing(string blank)
    {
        // The regression. Each of these reached `raw.Trim()[0]` and threw
        // IndexOutOfRangeException -- an unhandled 500 from a guard that was
        // written one line too late to ever fire.
        await using var db = CreateDb();
        var user = SeedClient(db);
        db.Skills.Add(new Skill { Id = 7, Name = "Python", CategoryId = null });
        await db.SaveChangesAsync();

        var project = await CreateProject(db, user, blank, "Python", blank);

        var saved = await db.ProjectSkills
            .Where(ps => ps.ProjectId == project.Id)
            .ToListAsync();

        saved.Should().HaveCount(1, "blanks are skipped, not stored and not fatal");
        saved[0].SkillId.Should().Be(7);
    }

    [Fact]
    public async Task Three_Blank_Skills_Create_A_Project_Rather_Than_A_500()
    {
        // The exact payload the old client schema let through: every old rule
        // permitted whitespace, and `.min(3)` counts array length, not content.
        await using var db = CreateDb();
        var user = SeedClient(db);

        var act = async () => await CreateProject(db, user, "  ", "   ", "\t");

        await act.Should().NotThrowAsync();
    }

    [Theory]
    [InlineData("Underwater Basket Weaving")]
    [InlineData("n8n")]
    [InlineData("macOS")]
    [InlineData("Node.js")]
    [InlineData("iOS")]
    public async Task A_Custom_Skill_Is_Stored_Exactly_As_Typed(string typed)
    {
        // The second server-side bug, and it was invisible until this test
        // asserted the casing rather than just the linkage.
        //
        // The loop title-cased every unmatched name --
        // `char.ToUpperInvariant(name[0]) + name[1..].ToLowerInvariant()` -- so
        // "Underwater Basket Weaving" was stored as "Underwater basket weaving"
        // and "macOS" as "Macos". The normalisation served no purpose: the lookup
        // lowercases both sides, so it never affected whether a skill matched.
        // It only altered the name a freelancer reads when deciding whether to
        // apply, and the client's own chips and AI badge both promise it as shown.
        await using var db = CreateDb();
        var user = SeedClient(db);

        var project = await CreateProject(db, user, typed);

        var saved = await db.ProjectSkills
            .Where(ps => ps.ProjectId == project.Id)
            .SingleAsync();

        saved.CustomName.Should().Be(typed);
    }

    [Fact]
    public async Task A_Padded_Real_Skill_Is_Matched_Rather_Than_Stored_As_Custom()
    {
        await using var db = CreateDb();
        var user = SeedClient(db);
        db.Skills.Add(new Skill { Id = 99, Name = "React", CategoryId = null });
        await db.SaveChangesAsync();

        var project = await CreateProject(db, user, "  React  ");

        var saved = await db.ProjectSkills
            .Where(ps => ps.ProjectId == project.Id)
            .SingleAsync();

        saved.SkillId.Should().Be(99, "surrounding whitespace is not part of the name");
    }

    // ── helpers ───────────────────────────────────────────────────────────

    private static async Task<ProjectResponse> CreateProject(
        WebOfWorkDbContext db, User user, params string[] skills)
    {
        // Flushed here rather than in each test. The service resolves the client
        // profile with a query, so an unsaved seed is invisible and the failure
        // reads as "Client profile not found" -- which is about the fixture, not
        // about the behaviour under test.
        await db.SaveChangesAsync();

        return await new ProjectService(db).CreateProjectAsync(
            user.Id,
            new CreateProjectRequest
            {
                Title = "Build a Modern Responsive Website",
                Description = "A responsive website that works on every device.",
                Requirements = "Must be tested across breakpoints.",
                ExpectedDeliverables = "Source code and a deployed site.",
                // `CategoryId` is validated by the service ("Category is
                // required") before any skill is looked at, so the fixture has to
                // carry one. No category row is needed: nothing here resolves it.
                CategoryId = 1,
                PricingType = "fixed",
                FixedPrice = 1500,
                DeliveryDays = 14,
                Skills = skills.ToList()
            });
    }

    private static User SeedClient(WebOfWorkDbContext db)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = $"client-{Guid.NewGuid()}@example.com",
            FullName = "Client",
            PasswordHash = "hash",
            Role = UserRole.Client,
            Status = UserStatus.Active,
            IsEmailVerified = true
        };
        db.Users.Add(user);
        db.ClientProfiles.Add(new ClientProfile
        {
            Id = Guid.NewGuid(),
            User = user,
            UserId = user.Id,
            CompanyName = "Acme"
        });
        return user;
    }

    private static WebOfWorkDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<WebOfWorkDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
}