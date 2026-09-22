using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace WebOfWork.Api.Jobs;

public class CleanupExpiredOtpsJob
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<CleanupExpiredOtpsJob> _logger;

    public CleanupExpiredOtpsJob(IConnectionMultiplexer redis, ILogger<CleanupExpiredOtpsJob> logger)
    {
        _redis = redis;
        _logger = logger;
    }

    public async Task ExecuteAsync(CancellationToken ct = default)
    {
        var endpoint = _redis.GetEndPoints().FirstOrDefault();
        if (endpoint == null)
        {
            _logger.LogWarning("CleanupExpiredOtpsJob skipped because no Redis endpoint is configured.");
            return;
        }

        var hostPort = endpoint.ToString().Split(':');
        var host = hostPort[0];
        var port = hostPort.Length > 1 && int.TryParse(hostPort[1], out var parsedPort) ? parsedPort : 6379;
        var server = _redis.GetServer(host, port);

        var patterns = new[] { "otp:*", "reg:*", "reset:*", "phone:*", "del:*", "cooldown:*" };
        foreach (var pattern in patterns)
        {
            foreach (var key in server.Keys(database: 0, pattern: pattern))
            {
                ct.ThrowIfCancellationRequested();
                if (await _redis.GetDatabase().KeyExistsAsync(key))
                {
                    await _redis.GetDatabase().KeyDeleteAsync(key);
                }
            }
        }

        _logger.LogInformation("CleanupExpiredOtpsJob completed.");
    }
}
