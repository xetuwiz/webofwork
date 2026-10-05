using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using WebOfWork.Api.Data;
using WebOfWork.Api.DTOs.Reviews;
using WebOfWork.Api.Models;
using WebOfWork.Api.Models.Enums;

namespace WebOfWork.Api.Services;

public class ReviewService : IReviewService
{
    private readonly WebOfWorkDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly INotificationPublisher? _notificationPublisher;

    public ReviewService(WebOfWorkDbContext db, ICurrentUser currentUser, INotificationPublisher? notificationPublisher = null)
    {
        _db = db;
        _currentUser = currentUser;
        _notificationPublisher = notificationPublisher;
    }

    public async Task<ReviewResponse> CreateReviewAsync(CreateReviewRequest request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.Id!.Value;
        // Role is stored as string in JWT claims e.g. "Client", "Freelancer", "Admin"
        var roleStr = _currentUser.Role ?? string.Empty;

        var contract = await _db.Contracts
            .Include(c => c.Client).ThenInclude(cp => cp.User)
            .Include(c => c.Freelancer).ThenInclude(fp => fp.User)
            .FirstOrDefaultAsync(c => c.Id == request.ContractId, cancellationToken)
            ?? throw new KeyNotFoundException("Contract not found.");

        // Step 11.21: Contract must be completed (or refunded - ref: Django view)
        if (contract.Status != ContractStatus.Completed && contract.Status != ContractStatus.Refunded)
            throw new ArgumentException("You can only leave a review for a completed contract.");

        // Step 11.23: No self-review
        if (contract.Client.UserId == userId && contract.Freelancer.UserId == userId)
            throw new ArgumentException("You cannot review yourself.");

        // Determine review type and reviewee based on current user's role in contract
        ReviewType reviewType;
        Guid revieweeId;

        if (roleStr == "Client" || roleStr == "1")
        {
            if (contract.Client.UserId != userId)
                throw new UnauthorizedAccessException("Not your contract.");
            reviewType = ReviewType.Freelancer;
            revieweeId = contract.Freelancer.UserId;
        }
        else if (roleStr == "Freelancer" || roleStr == "2")
        {
            if (contract.Freelancer.UserId != userId)
                throw new UnauthorizedAccessException("Not your contract.");
            reviewType = ReviewType.Client;
            revieweeId = contract.Client.UserId;
        }
        else
        {
            throw new UnauthorizedAccessException("Only contract participants can review.");
        }

        // Step 11.20: No duplicate review for same contract+type
        var alreadyExists = await _db.Reviews.AnyAsync(
            r => r.ReviewerId == userId && r.ContractId == request.ContractId && r.Type == reviewType,
            cancellationToken);
        if (alreadyExists)
            throw new ArgumentException("You have already submitted a review for this contract.");

        var review = new Review
        {
            ContractId = request.ContractId,
            ReviewerId = userId,
            RevieweeId = revieweeId,
            Type = reviewType,
            Rating = request.Rating,
            Comment = request.Comment,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.Reviews.Add(review);
        await _db.SaveChangesAsync(cancellationToken);

        // Step 11.28: Recalculate average rating inline (mirrors Python signal)
        await RecalculateRatingAsync(revieweeId, reviewType, cancellationToken);

        if (_notificationPublisher != null)
        {
            await _notificationPublisher.PublishAsync(
                revieweeId,
                NotificationType.ReviewReceived,
                $"You received a new review for contract #{request.ContractId}.",
                "Review",
                review.Id,
                cancellationToken);
        }

        return MapToResponse(review);
    }

    public async Task<List<ReviewResponse>> GetFreelancerReviewsAsync(CancellationToken cancellationToken)
    {
        var userId = _currentUser.Id!.Value;
        var reviews = await _db.Reviews
            .Include(r => r.Reviewer).Include(r => r.Contract).ThenInclude(c => c!.Project)
            .Where(r => r.RevieweeId == userId && r.Type == ReviewType.Freelancer)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(cancellationToken);

        return reviews.Select(MapToResponse).ToList();
    }

    public async Task<List<ReviewResponse>> GetClientReviewsAsync(CancellationToken cancellationToken)
    {
        var userId = _currentUser.Id!.Value;
        var reviews = await _db.Reviews
            .Include(r => r.Reviewer).Include(r => r.Contract).ThenInclude(c => c!.Project)
            .Where(r => r.RevieweeId == userId && r.Type == ReviewType.Client)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(cancellationToken);

        return reviews.Select(MapToResponse).ToList();
    }

    public async Task<List<ReviewResponse>> GetFreelancerReviewsByIdAsync(Guid freelancerProfileId, CancellationToken cancellationToken)
    {
        var profile = await _db.FreelancerProfiles
            .FirstOrDefaultAsync(p => p.Id == freelancerProfileId, cancellationToken);
        if (profile == null) throw new KeyNotFoundException("Freelancer profile not found.");

        var reviews = await _db.Reviews
            .Include(r => r.Reviewer).Include(r => r.Contract).ThenInclude(c => c!.Project)
            .Where(r => r.RevieweeId == profile.UserId && r.Type == ReviewType.Freelancer)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(cancellationToken);

        return reviews.Select(MapToResponse).ToList();
    }

    public async Task<List<ReviewResponse>> GetAllReviewsAsync(CancellationToken cancellationToken)
    {
        var reviews = await _db.Reviews.Include(r => r.Reviewer).Include(r => r.Contract).ThenInclude(c => c!.Project).AsNoTracking().OrderByDescending(r => r.CreatedAt).ToListAsync(cancellationToken);
        return reviews.Select(MapToResponse).ToList();
    }


    public async Task DeleteReviewAsync(int id, CancellationToken cancellationToken)
    {
        var review = await _db.Reviews.FindAsync(new object[] { id }, cancellationToken)
            ?? throw new KeyNotFoundException("Review not found.");

        var revieweeId = review.RevieweeId;
        var reviewType = review.Type;

        _db.Reviews.Remove(review);
        await _db.SaveChangesAsync(cancellationToken);

        await RecalculateRatingAsync(revieweeId, reviewType, cancellationToken);
    }

    private async Task RecalculateRatingAsync(Guid revieweeId, ReviewType reviewType, CancellationToken cancellationToken)
    {
        var stats = await _db.Reviews
            .Where(r => r.RevieweeId == revieweeId && r.Type == reviewType)
            .GroupBy(_ => 1)
            .Select(g => new { Avg = g.Average(r => (double)r.Rating), Count = g.Count() })
            .FirstOrDefaultAsync(cancellationToken);

        var avg = stats != null ? (decimal)stats.Avg : 0m;
        var count = stats?.Count ?? 0;

        if (reviewType == ReviewType.Freelancer)
        {
            var fpProfile = await _db.FreelancerProfiles.FirstOrDefaultAsync(p => p.UserId == revieweeId, cancellationToken);
            if (fpProfile != null)
            {
                fpProfile.AverageRating = Math.Round(avg, 2);
                fpProfile.ReviewCount = count;
            }
        }
        else
        {
            var cpProfile = await _db.ClientProfiles.FirstOrDefaultAsync(p => p.UserId == revieweeId, cancellationToken);
            if (cpProfile != null)
            {
                cpProfile.AverageRating = Math.Round(avg, 2);
                cpProfile.ReviewCount = count;
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<WebOfWork.Api.DTOs.Common.PagedResponse<ReviewResponse>> GetClientReviewsPagedAsync(Guid revieweeId, int page, int pageSize, CancellationToken cancellationToken)
    {
        var p = Math.Max(page, 1);
        var size = Math.Clamp(pageSize, 1, 100);
        var query = _db.Reviews
            .Include(r => r.Reviewer).Include(r => r.Contract).ThenInclude(c => c!.Project)
            .Include(r => r.Reviewer)
            .Where(r => r.RevieweeId == revieweeId && r.Type == ReviewType.Client)
            .OrderByDescending(r => r.CreatedAt);
        var count = await query.CountAsync(cancellationToken);
        var items = await query.Skip((p - 1) * size).Take(size).ToListAsync(cancellationToken);
        return new WebOfWork.Api.DTOs.Common.PagedResponse<ReviewResponse>
        {
            Results = items.Select(MapToResponse).ToList(),
            Count = count,
            Page = p,
            PageSize = size
        };
    }

    private static ReviewResponse MapToResponse(Review r) => new()
    {
        Id = r.Id,
        ContractId = r.ContractId,
        Contract = r.ContractId,
        ReviewerId = r.ReviewerId,
        Reviewer = r.ReviewerId,
        RevieweeId = r.RevieweeId,
        Reviewee = r.RevieweeId,
        Type = r.Type,
        Rating = r.Rating,
        Comment = r.Comment,
        CreatedAt = r.CreatedAt,
        ReviewerName = r.Reviewer?.FullName ?? string.Empty,
        ReviewerPhoto = r.Reviewer?.ProfilePhoto,
        ProjectTitle = r.Contract?.Project?.Title ?? string.Empty
    };
}
