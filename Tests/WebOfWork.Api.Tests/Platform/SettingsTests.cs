using System;
using System.Threading.Tasks;
using Xunit;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using WebOfWork.Api.Data;
using WebOfWork.Api.Models;
using WebOfWork.Api.DTOs.Platform;
using WebOfWork.Api.Services;
using Testcontainers.PostgreSql;

namespace WebOfWork.Api.Tests.Platform;

[Trait("Category", "Container")]
public class PlatformTestFixture : IAsyncLifetime
{
    public PostgreSqlContainer Postgres { get; } = new PostgreSqlBuilder().WithImage("postgres:15-alpine").Build();

    public async Task InitializeAsync()
    {
        await Postgres.StartAsync();
    }

    public async Task DisposeAsync()
    {
        await Postgres.DisposeAsync();
    }
}

[CollectionDefinition("Platform Collection")]
[Trait("Category", "Container")]
public class PlatformCollection : ICollectionFixture<PlatformTestFixture> { }

[Collection("Platform Collection")]
public class SettingsTests
{
    private readonly PlatformTestFixture _fixture;

    public SettingsTests(PlatformTestFixture fixture)
    {
        _fixture = fixture;
    }

    private WebOfWorkDbContext GetDbContext()
    {
        var options = new DbContextOptionsBuilder<WebOfWorkDbContext>()
            .UseSnakeCaseNamingConvention().UseNpgsql(_fixture.Postgres.GetConnectionString())
            .Options;
            
        var context = new WebOfWorkDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }

    [Fact]
    public async Task UpdateSettings_WithDifferentCurrency_ThrowsArgumentException_ResultingIn400()
    {
        await using var context = GetDbContext();
        var service = new ContentService(context);
        
        var request = new UpdateGlobalSettingsRequest(
            null, null, null, null, null, null, 
            DefaultCurrency: "EUR" 
        );

        Func<Task> act = async () => await service.UpdateGlobalSettingsAsync(request);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*DefaultCurrency cannot be changed from USD.*");
    }

    [Fact]
    /// <summary>
    /// The getter must not write.
    /// </summary>
    /// <remarks>
    /// This test used to be called <c>GetSettings_SeedsSingletonRow_IfMissing</c>
    /// and asserted that reading settings on an empty table left one row behind.
    /// It failed, permanently, and the code was right.
    ///
    /// <c>ContentService.GetGlobalSettingsAsync</c> used to insert the row when it
    /// was missing, so a plain <c>GET</c> wrote to the database. That was fixed --
    /// a read that mutates is its own defect -- and the fix made the getter return
    /// a detached <c>new GlobalSettings { Id = 1 }</c> instead. The test was never
    /// updated, so it went on asserting the behaviour that had been removed on
    /// purpose, and failed on every run from then on.
    ///
    /// The assertion below is the contract that replaced it, and it is the more
    /// valuable of the two: it pins the *absence* of a side effect, which is the
    /// thing worth protecting. A test that requires a write cannot catch the
    /// regression it was originally written to prevent.
    /// </remarks>
    public async Task GetSettings_ReturnsDefaults_AndWritesNothing_WhenRowMissing()
    {
        await using var context = GetDbContext();
        var service = new ContentService(context);

        context.GlobalSettings.RemoveRange(context.GlobalSettings);
        await context.SaveChangesAsync();

        var settings = await service.GetGlobalSettingsAsync();

        settings.Should().NotBeNull();
        settings.Id.Should().Be(1);
        settings.DefaultCurrency.Should().Be("USD");

        // The point of the test: reading must not have created anything.
        var dbCount = await context.GlobalSettings.CountAsync();
        dbCount.Should().Be(0);
    }

    /// <summary>
    /// A second read is as harmless as the first.
    /// </summary>
    /// <remarks>
    /// Guards the obvious wrong fix for the test above: making the getter insert
    /// the row so that <c>CountAsync()</c> returns 1. That satisfies a
    /// count-equals-one assertion while reintroducing the write, so the assertion
    /// has to be about the absence of the write, not about its presence.
    /// </remarks>
    [Fact]
    public async Task GetSettings_CalledTwice_StillWritesNothing()
    {
        await using var context = GetDbContext();
        var service = new ContentService(context);

        context.GlobalSettings.RemoveRange(context.GlobalSettings);
        await context.SaveChangesAsync();

        await service.GetGlobalSettingsAsync();
        var second = await service.GetGlobalSettingsAsync();

        second.Id.Should().Be(1);
        (await context.GlobalSettings.CountAsync()).Should().Be(0);
    }

    /// <summary>
    /// When the row does exist, it is returned as stored.
    /// </summary>
    /// <remarks>
    /// The counterpart to the above. "Returns a default when missing" is only half
    /// the contract; if the default were returned unconditionally then a correctly
    /// seeded database would be silently ignored.
    /// </remarks>
    [Fact]
    public async Task GetSettings_ReturnsTheStoredRow_WhenItExists()
    {
        await using var context = GetDbContext();
        var service = new ContentService(context);

        context.GlobalSettings.RemoveRange(context.GlobalSettings);
        await context.SaveChangesAsync();

        // Seeded through SQL rather than through the model. `DefaultCurrency` has a
        // private setter by design -- it is pinned to USD and
        // `UpdateGlobalSettingsAsync` throws on any other value -- so the only way
        // to construct a row at all is SQL. Every NOT NULL column is named, because
        // a partially-specified insert fails on the first omission and that says
        // nothing about the behaviour under test.
        await context.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO global_settings
                (id, support_email, commission_percentage, pay_pal_email,
                 min_brief_confidence, proposal_screening_threshold,
                 match_score_threshold, default_currency, created_at, updated_at)
            VALUES (1, 'support@webofwork.lk', 10.00, 'payments@webofwork.lk',
                    0.60, 0.50, 0.60, 'USD', now(), now())
            """);

        var settings = await service.GetGlobalSettingsAsync();

        settings.Id.Should().Be(1);
        settings.DefaultCurrency.Should().Be("USD");
        (await context.GlobalSettings.CountAsync()).Should().Be(1);
    }
}
