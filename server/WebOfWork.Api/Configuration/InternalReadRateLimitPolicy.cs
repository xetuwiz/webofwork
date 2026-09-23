using System.Threading.RateLimiting;

namespace WebOfWork.Api.Configuration;

public static class InternalReadRateLimitPolicy
{
    public const string PolicyName = "internal-read";
    public const int PermitLimit = 60;

    public static RateLimitPartition<string> Create(HttpContext _)
        => RateLimitPartition.GetFixedWindowLimiter(
            "internal-read",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = PermitLimit,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            });
}