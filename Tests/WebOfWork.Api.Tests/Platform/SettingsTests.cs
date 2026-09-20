using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using WebOfWork.Api.Data;
using WebOfWork.Api.DTOs.Platform;
using WebOfWork.Api.Models;
using WebOfWork.Api.Services;
using Xunit;

namespace WebOfWork.Api.Tests.Platform;

public class SettingsTests
{
    private WebOfWorkDbContext GetDbContext()
    {
        var options = new DbContextOptionsBuilder<WebOfWorkDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
            
        var context = new WebOfWorkDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }

    [Fact]
    public async Task UpdateSettings_WithDifferentCurrency_ThrowsArgumentException_ResultingIn400()
    {
        // Arrange
        await using var context = GetDbContext();
        var service = new ContentService(context);
        
        var request = new UpdateGlobalSettingsRequest(
            null, null, null, null, null, null, 
            DefaultCurrency: "USD" // Different currency
        );

        // Act
        Func<Task> act = async () => await service.UpdateGlobalSettingsAsync(request);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*DefaultCurrency cannot be changed from LKR.*");
    }

    [Fact]
    public async Task GetSettings_SeedsSingletonRow_IfMissing()
    {
        // Arrange
        await using var context = GetDbContext();
        var service = new ContentService(context);

        // Ensure empty
        context.GlobalSettings.RemoveRange(context.GlobalSettings);
        await context.SaveChangesAsync();

        // Act
        var settings = await service.GetGlobalSettingsAsync();

        // Assert
        settings.Should().NotBeNull();
        settings.Id.Should().Be(1);
        settings.DefaultCurrency.Should().Be("LKR");
        
        var dbCount = await context.GlobalSettings.CountAsync();
        dbCount.Should().Be(1);
    }
}
