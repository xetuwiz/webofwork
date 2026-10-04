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

        var freelancerIds = freelancerStats.Select(s => s.RevieweeId).ToList();
        var freelancerProfiles = await _db.FreelancerProfiles
            .Where(p => freelancerIds.Contains(p.UserId))
            .ToDictionaryAsync(p => p.UserId, cancellationToken);

        foreach (var stat in freelancerStats)
        {
            if (freelancerProfiles.TryGetValue(stat.RevieweeId, out var profile))
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

        var clientIds = clientStats.Select(s => s.RevieweeId).ToList();
        var clientProfiles = await _db.ClientProfiles
            .Where(p => clientIds.Contains(p.UserId))
            .ToDictionaryAsync(p => p.UserId, cancellationToken);

        foreach (var stat in clientStats)
        {
            if (clientProfiles.TryGetValue(stat.RevieweeId, out var profile))
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
