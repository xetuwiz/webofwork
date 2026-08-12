namespace WebOfWork.Api.Configuration;

public sealed class RedisOptions
{
    public const string Section = "Redis";

    /// <summary>StackExchange.Redis connection string, e.g. "localhost:6379".</summary>
    public string ConnectionString { get; init; } = string.Empty;
}
