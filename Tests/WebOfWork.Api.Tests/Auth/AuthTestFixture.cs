using System.Threading.Tasks;
using Xunit;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;

namespace WebOfWork.Api.Tests.Auth;

public class AuthTestFixture : IAsyncLifetime
{
    public PostgreSqlContainer Postgres { get; } = new PostgreSqlBuilder().WithImage("postgres:15-alpine").Build();
    public RedisContainer Redis { get; } = new RedisBuilder().WithImage("redis:7-alpine").Build();

    public async Task InitializeAsync()
    {
        await Postgres.StartAsync();
        await Redis.StartAsync();
    }

    public async Task DisposeAsync()
    {
        await Postgres.DisposeAsync();
        await Redis.DisposeAsync();
    }
}

[CollectionDefinition("Auth E2E Collection")]
public class AuthE2ECollection : ICollectionFixture<AuthTestFixture> { }
