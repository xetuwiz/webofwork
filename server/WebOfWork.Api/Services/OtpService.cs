using StackExchange.Redis;

namespace WebOfWork.Api.Services;

public interface IOtpService
{
    Task SetOtpAsync(string email, string otp, TimeSpan expiry);
    Task<string?> GetOtpAsync(string email);
    Task DeleteOtpAsync(string email);
    Task SetCooldownAsync(string email, TimeSpan cooldown);
    Task<bool> HasCooldownAsync(string email);
}

public class OtpService : IOtpService
{
    private readonly IConnectionMultiplexer _redis;

    public OtpService(IConnectionMultiplexer redis)
    {
        _redis = redis;
    }

    public async Task SetOtpAsync(string email, string otp, TimeSpan expiry)
    {
        var db = _redis.GetDatabase();
        var key = $"otp:{email.ToLowerInvariant()}";
        await db.StringSetAsync(key, otp, expiry);
    }

    public async Task<string?> GetOtpAsync(string email)
    {
        var db = _redis.GetDatabase();
        var key = $"otp:{email.ToLowerInvariant()}";
        var val = await db.StringGetAsync(key);
        return val.HasValue ? val.ToString() : null;
    }

    public async Task DeleteOtpAsync(string email)
    {
        var db = _redis.GetDatabase();
        var key = $"otp:{email.ToLowerInvariant()}";
        await db.KeyDeleteAsync(key);
    }

    public async Task SetCooldownAsync(string email, TimeSpan cooldown)
    {
        var db = _redis.GetDatabase();
        var key = $"cooldown:{email.ToLowerInvariant()}";
        await db.StringSetAsync(key, "locked", cooldown);
    }

    public async Task<bool> HasCooldownAsync(string email)
    {
        var db = _redis.GetDatabase();
        var key = $"cooldown:{email.ToLowerInvariant()}";
        return await db.KeyExistsAsync(key);
    }
}
