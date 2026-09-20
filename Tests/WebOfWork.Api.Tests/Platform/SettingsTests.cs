using System;
using System.Threading.Tasks;
using Xunit;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using WebOfWork.Api.Data;
using WebOfWork.Api.DTOs.Platform;
using WebOfWork.Api.Services;
using Testcontainers.PostgreSql;

namespace WebOfWork.Api.Tests.Platform;

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
            .UseNpgsql(_fixture.Postgres.GetConnectionString())
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
            DefaultCurrency: "USD" 
        );

        Func<Task> act = async () => await service.UpdateGlobalSettingsAsync(request);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*DefaultCurrency cannot be changed from LKR.*");
    }

    [Fact]
    public async Task GetSettings_SeedsSingletonRow_IfMissing()
    {
        await using var context = GetDbContext();
        var service = new ContentService(context);

        context.GlobalSettings.RemoveRange(context.GlobalSettings);
        await context.SaveChangesAsync();

        var settings = await service.GetGlobalSettingsAsync();

        settings.Should().NotBeNull();
        settings.Id.Should().Be(1);
        settings.DefaultCurrency.Should().Be("LKR");
        
        var dbCount = await context.GlobalSettings.CountAsync();
        dbCount.Should().Be(1);
    }
}
