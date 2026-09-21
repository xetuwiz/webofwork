using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WebOfWork.Api.Data;
using WebOfWork.Api.Models.Enums;

namespace WebOfWork.Api.Jobs;

/// <summary>
/// Step 11.28: Periodic job that recalculates all FreelancerProfile and ClientProfile
/// average ratings and review counts from the Reviews table.
/// Mirrors the Django post_save/post_delete signal in profiles/signals.py.
/// </summary>
public class RecalculateRatingsJob
{
    private readonly WebOfWorkDbContext _db;
    private readonly ILogger<RecalculateRatingsJob> _logger;

    public RecalculateRatingsJob(WebOfWorkDbContext db, ILogger<RecalculateRatingsJob> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("RecalculateRatingsJob started at {Time}", DateTime.UtcNow);

        // Freelancer ratings
        var freelancerStats = await _db.Reviews
            .Where(r => r.Type == ReviewType.Freelancer)
            .GroupBy(r => r.RevieweeId)
            .Select(g => new
            {
                RevieweeId = g.Key,
                Avg = g.Average(r => (double)r.Rating),
                Count = g.Count()
            })
            .ToListAsync(cancellationToken);

        foreach (var stat in freelancerStats)
        {
            var profile = await _db.FreelancerProfiles
                .FirstOrDefaultAsync(p => p.UserId == stat.RevieweeId, cancellationToken);

            if (profile != null)
            {
                profile.AverageRating = Math.Round((decimal)stat.Avg, 2);
                profile.ReviewCount = stat.Count;
            }
        }

        // Client ratings
        var clientStats = await _db.Reviews
            .Where(r => r.Type == ReviewType.Client)
            .GroupBy(r => r.RevieweeId)
            .Select(g => new
            {
                RevieweeId = g.Key,
                Avg = g.Average(r => (double)r.Rating),
                Count = g.Count()
            })
            .ToListAsync(cancellationToken);

        foreach (var stat in clientStats)
        {
            var profile = await _db.ClientProfiles
                .FirstOrDefaultAsync(p => p.UserId == stat.RevieweeId, cancellationToken);

            if (profile != null)
            {
                profile.AverageRating = Math.Round((decimal)stat.Avg, 2);
                profile.ReviewCount = stat.Count;
            }
        }

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("RecalculateRatingsJob completed at {Time}. Updated {Freelancers} freelancer profiles and {Clients} client profiles.",
            DateTime.UtcNow, freelancerStats.Count, clientStats.Count);
    }
}
