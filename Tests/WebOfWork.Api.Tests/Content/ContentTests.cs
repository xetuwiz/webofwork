using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using WebOfWork.Api.Data;
using WebOfWork.Api.Models;
using WebOfWork.Api.Services;
using Xunit;

namespace WebOfWork.Api.Tests.Content;

[Trait("Category", "Container")]
public sealed class ContentTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:15-alpine")
        .WithDatabase("webofwork_content_test")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    public Task InitializeAsync() => _postgres.StartAsync();

    public Task DisposeAsync() => _postgres.DisposeAsync().AsTask();

    [Fact]
    public async Task GetPublicFaqsAsync_ReturnsOnlyActiveFaqsInRequestedCategory()
    {
        await using var db = await CreateDbContextAsync();
        db.Faqs.AddRange(
            new Faq
            {
                Question = "Active client question",
                Answer = "Answer",
                Category = FaqCategory.Client,
                IsActive = true,
                SortOrder = 1
            },
            new Faq
            {
                Question = "Inactive client question",
                Answer = "Answer",
                Category = FaqCategory.Client,
                IsActive = false,
                SortOrder = 2
            },
            new Faq
            {
                Question = "Active payment question",
                Answer = "Answer",
                Category = FaqCategory.Payment,
                IsActive = true,
                SortOrder = 3
            });
        await db.SaveChangesAsync();

        var result = await new ContentService(db).GetPublicFaqsAsync(FaqCategory.Client, CancellationToken.None);

        result.Should().ContainSingle(item => item.Question == "Active client question");
        result.Should().NotContain(item => item.Question == "Inactive client question");
        result.Should().NotContain(item => item.Question == "Active payment question");
    }

    [Fact]
    public async Task GetPublicCmsByCategoryAsync_ReturnsOnlyActiveSectionsInRequestedCategory()
    {
        await using var db = await CreateDbContextAsync();
        db.CmsSections.AddRange(
            new CmsSection
            {
                Category = CmsSectionCategory.Terms,
                Heading = "Active terms",
                Description = "Terms",
                IsActive = true,
                SortOrder = 1
            },
            new CmsSection
            {
                Category = CmsSectionCategory.Terms,
                Heading = "Inactive terms",
                Description = "Terms",
                IsActive = false,
                SortOrder = 2
            },
            new CmsSection
            {
                Category = CmsSectionCategory.Privacy,
                Heading = "Active privacy",
                Description = "Privacy",
                IsActive = true,
                SortOrder = 3
            });
        await db.SaveChangesAsync();

        var result = await new ContentService(db).GetPublicCmsByCategoryAsync(CmsSectionCategory.Terms, CancellationToken.None);

        result.Should().ContainSingle(item => item.Heading == "Active terms");
        result.Should().NotContain(item => item.Heading == "Inactive terms");
        result.Should().NotContain(item => item.Heading == "Active privacy");
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