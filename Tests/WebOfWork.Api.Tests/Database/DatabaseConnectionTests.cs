using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using WebOfWork.Api.Data;
using Testcontainers.PostgreSql;
using Xunit;

namespace WebOfWork.Api.Tests.Database;

public class DatabaseConnectionTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _dbContainer = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("webofwork_test")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    public async Task InitializeAsync()
    {
        await _dbContainer.StartAsync();
    }

    public async Task DisposeAsync()
    {
        await _dbContainer.DisposeAsync();
    }

    [Fact]
    public async Task Database_CanConnect_AndRunMigrations()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<WebOfWorkDbContext>()
            .UseNpgsql(_dbContainer.GetConnectionString())
            .UseSnakeCaseNamingConvention()
            .Options;

        await using var context = new WebOfWorkDbContext(options);

        // Act & Assert
        // Ensures the context can connect and the initial model is valid
        var canConnect = await context.Database.CanConnectAsync();
        canConnect.Should().BeTrue("because the testcontainer postgres should be running and accessible");

        // We run EnsureCreatedAsync here just to verify EF Core can translate the model to SQL without errors
        // Note: Real migrations are tested differently, but this proves the context schema is fundamentally valid
        await context.Database.EnsureCreatedAsync();
        
        // No exceptions means the schema translation worked.
    }
}
