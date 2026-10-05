using System.Reflection;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using WebOfWork.Api.Controllers;
using WebOfWork.Api.Data;
using WebOfWork.Api.DTOs.Internal;
using WebOfWork.Api.Models;
using WebOfWork.Api.Services;
using Xunit;

namespace WebOfWork.Api.Tests.AiWorkflow;

/// <summary>
/// The reference price bands the drafting agent quotes from.
/// </summary>
/// <remarks>
/// <para>
/// These exist because the drafter had no way to answer "what does this usually
/// cost?" and so declined to. A client who posted a brief with no budget got
/// <c>"no price is suggested"</c> and a shrug -- the whole value of a drafting
/// assistant, withdrawn. And it failed quietly: a missing capability and a
/// considered decision produce the same output, so nothing looked broken.
/// </para>
/// <para>
/// The bands are advertised reference ranges from a marketplace's public pages,
/// not quotes, and observed freelancer rates on that marketplace run far outside
/// them at both ends. So the assertions here are about the shape of what is
/// served -- bands, attributed, filterable -- and never about a particular figure.
/// </para>
/// </remarks>
public class PriceBandTests
{
    private static WebOfWorkDbContext Db(string name)
    {
        var options = new DbContextOptionsBuilder<WebOfWorkDbContext>()
            .UseInMemoryDatabase(name)
            .Options;
        return new WebOfWorkDbContext(options);
    }

    private static Mock<IInternalReadAuditService> Audit()
    {
        // The real audit service logs a line per read. Not what is under test,
        // and a null IDisposable return would fault the controller's `using`.
        var audit = new Mock<IInternalReadAuditService>();
        audit.Setup(x => x.Begin(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Mock.Of<IDisposable>());
        return audit;
    }

    private static async Task SeedAsync(WebOfWorkDbContext db)
    {
        var development = new Category { Name = "Development" };
        var design = new Category { Name = "Design" };
        db.Categories.AddRange(development, design);
        await db.SaveChangesAsync();

        db.MarketPriceReferences.AddRange(
            new MarketPriceReference
            {
                CategoryId = development.Id,
                Deliverable = "Landing page",
                Level = "intermediate",
                MinPriceUsd = 1500,
                MaxPriceUsd = 5000,
                Source = "reference catalogue",
            },
            new MarketPriceReference
            {
                CategoryId = design.Id,
                Deliverable = "Logo design",
                Level = "entry-mid",
                MinPriceUsd = 150,
                MaxPriceUsd = 500,
                Source = "reference catalogue",
            },
            // Cross-cutting: no category, so it must survive a category filter.
            new MarketPriceReference
            {
                CategoryId = null,
                Deliverable = "AI readiness audit",
                Level = "entry-intermediate",
                MinPriceUsd = 1500,
                MaxPriceUsd = 4000,
                Source = "reference catalogue",
            });
        await db.SaveChangesAsync();
    }

    private static InternalReadController Controller(WebOfWorkDbContext db)
        => new(
            Mock.Of<IContentService>(),
            new InternalReadService(db),
            Audit().Object);

    /// <summary>
    /// Unwrap an <c>ActionResult&lt;T&gt;</c> down to the rows.
    /// </summary>
    /// <remarks>
    /// The action is declared <c>ActionResult&lt;IReadOnlyList&lt;...&gt;&gt;</c>, so
    /// a success is an <see cref="OkObjectResult"/> nested inside the wrapper
    /// rather than the result itself. Asserting on the outer type would make this
    /// helper fail for a reason that has nothing to do with the bands.
    /// </remarks>
    private static IReadOnlyList<InternalPriceBandDto> Bands<T>(ActionResult<T> result)
    {
        var inner = result.Result;
        inner.Should().BeOfType<OkObjectResult>();
        return ((OkObjectResult)inner!).Value.Should()
            .BeAssignableTo<IReadOnlyList<InternalPriceBandDto>>().Subject;
    }

    [Fact]
    public async Task The_bands_are_served_as_low_high_ranges_with_their_source()
    {
        await using var db = Db(nameof(The_bands_are_served_as_low_high_ranges_with_their_source));
        await SeedAsync(db);

        var rows = Bands(await Controller(db).GetPriceBands(null, null, null, default));

        rows.Should().HaveCount(3);
        rows.Should().OnlyContain(x => x.MinPriceUsd <= x.MaxPriceUsd,
            "a band whose low end exceeds its high end is a typo, not a range");
        rows.Should().OnlyContain(x => !string.IsNullOrWhiteSpace(x.Source),
            "a price the client cannot trace is a price they will not post");
    }

    /// <summary>
    /// A single figure, however convenient, is the thing this data cannot support.
    /// </summary>
    [Fact]
    public async Task No_row_exposes_a_single_price()
    {
        await using var db = Db(nameof(No_row_exposes_a_single_price));
        await SeedAsync(db);

        var rows = Bands(await Controller(db).GetPriceBands(null, null, null, default));

        foreach (var row in rows)
        {
            var properties = typeof(InternalPriceBandDto)
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Select(p => p.Name.ToLowerInvariant())
                .ToList();

            properties.Should().NotContain("price");
            properties.Should().NotContain("recommendedprice");
            properties.Should().NotContain("quote");
        }
    }

    [Theory]
    [InlineData("snake")]
    [InlineData("camel")]
    public async Task A_category_filter_narrows_the_list_whichever_spelling_is_used(string spelling)
    {
        await using var db = Db(
            $"bands-{spelling}-{nameof(A_category_filter_narrows_the_list_whichever_spelling_is_used)}");
        await SeedAsync(db);

        var rows = Bands(await Controller(db).GetPriceBands(
            categoryIdSnake: spelling == "snake" ? 1 : null,
            categoryIdCamel: spelling == "camel" ? 1 : null,
            search: null,
            ct: default));

        // The Development band, plus the cross-cutting one. Not the Design band.
        rows.Should().HaveCount(2);
        rows.Should().NotContain(x => x.Deliverable == "Logo design");
    }

    /// <summary>
    /// The snake_case spelling is the project's own wire convention, and a
    /// <c>[FromQuery] categoryId</c> is reachable only as <c>?categoryId=</c>:
    /// <c>SnakeCaseLower</c> governs JSON, not query-string binding. Binding only
    /// the camelCase name means the conventional caller silently receives
    /// everything -- the wrong answer, looking like the right one.
    /// </summary>
    [Fact]
    public async Task The_conventional_snake_case_filter_is_not_silently_ignored()
    {
        await using var db = Db(nameof(The_conventional_snake_case_filter_is_not_silently_ignored));
        await SeedAsync(db);

        var controller = Controller(db);

        var filtered = Bands(await controller.GetPriceBands(1, null, null, default));
        var unfiltered = Bands(await controller.GetPriceBands(null, null, null, default));

        filtered.Count.Should().BeLessThan(unfiltered.Count,
            "a filter that returns everything is not a filter");
    }

    [Fact]
    public async Task Cross_cutting_bands_survive_a_category_filter()
    {
        await using var db = Db(nameof(Cross_cutting_bands_survive_a_category_filter));
        await SeedAsync(db);

        var rows = Bands(await Controller(db).GetPriceBands(null, null, null, default));

        var crossCutting = rows.Single(x => x.Deliverable == "AI readiness audit");
        crossCutting.CategoryId.Should().BeNull();
    }

    [Fact]
    public async Task A_search_narrows_by_deliverable_name()
    {
        await using var db = Db(nameof(A_search_narrows_by_deliverable_name));
        await SeedAsync(db);

        var rows = Bands(await Controller(db).GetPriceBands(null, null, "logo", default));

        rows.Should().ContainSingle();
        rows[0].Deliverable.Should().Be("Logo design");
    }

    [Fact]
    public async Task The_category_name_travels_with_the_band()
    {
        await using var db = Db(nameof(The_category_name_travels_with_the_band));
        await SeedAsync(db);

        var rows = Bands(await Controller(db).GetPriceBands(null, null, null, default));

        // Denormalised so the agent never needs a second round trip to say which
        // category a band belongs to.
        rows.Should().Contain(x => x.Deliverable == "Landing page" && x.CategoryName == "Development");
        var crossCutting = rows.Single(x => x.Deliverable == "AI readiness audit");
        crossCutting.CategoryName.Should().BeNull();
    }
}