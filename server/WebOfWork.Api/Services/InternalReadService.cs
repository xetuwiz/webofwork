using Microsoft.EntityFrameworkCore;
using WebOfWork.Api.Data;
using WebOfWork.Api.DTOs.Internal;
using WebOfWork.Api.Models;

namespace WebOfWork.Api.Services;

public interface IInternalReadService
{
    Task<InternalProjectDto?> GetProjectAsync(int projectId, CancellationToken ct = default);
    Task<IReadOnlyList<InternalFreelancerListDto>> SearchFreelancersAsync(string? query, int? categoryId, decimal? minRating, bool? available, int limit = 50, CancellationToken ct = default);
    Task<InternalFreelancerProfileDto?> GetFreelancerProfileAsync(Guid freelancerId, CancellationToken ct = default);
    Task<IReadOnlyList<InternalReviewDto>> GetFreelancerReviewsAsync(Guid freelancerId, CancellationToken ct = default);
    Task<IReadOnlyList<InternalPackageDto>> GetFreelancerPackagesAsync(Guid freelancerId, CancellationToken ct = default);
    Task<InternalCommissionDto> GetCommissionAsync(CancellationToken ct = default);
    Task<IReadOnlyList<InternalDisputeDto>> GetContractDisputesAsync(int contractId, CancellationToken ct = default);
    Task<InternalBriefDto?> GetBriefAsync(int briefId, CancellationToken ct = default);
}

public sealed class InternalReadService : IInternalReadService
{
    private readonly WebOfWorkDbContext _db;

    public InternalReadService(WebOfWorkDbContext db)
    {
        _db = db;
    }

    public async Task<InternalProjectDto?> GetProjectAsync(int projectId, CancellationToken ct = default)
    {
        var project = await _db.Projects.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == projectId, ct);
        if (project is null) return null;

        return new InternalProjectDto(
            project.Id,
            project.Title,
            project.Description,
            project.Requirements,
            project.ExpectedDeliverables,
            project.PricingType.ToString(),
            project.FixedPrice,
            project.MinPrice,
            project.MaxPrice,
            project.ExpectedDays,
            project.Status.ToString(),
            project.ClientId);
    }

    public async Task<IReadOnlyList<InternalFreelancerListDto>> SearchFreelancersAsync(
        string? query,
        int? categoryId,
        decimal? minRating,
        bool? available,
        int limit = 50,
        CancellationToken ct = default)
    {
        limit = Math.Clamp(limit, 1, 50);
        var normalizedQuery = query?.Trim();
        var profilesQuery = _db.FreelancerProfiles.AsNoTracking()
            .Where(x => x.IsActive && (!available.HasValue || x.Availability == available.Value));

        if (categoryId.HasValue)
            profilesQuery = profilesQuery.Where(x => x.CategoryId == categoryId.Value);
        if (minRating.HasValue)
            profilesQuery = profilesQuery.Where(x => x.AverageRating >= minRating.Value);
        if (!string.IsNullOrWhiteSpace(normalizedQuery))
        {
            profilesQuery = profilesQuery.Where(x =>
                x.Tagline.Contains(normalizedQuery) ||
                x.Bio.Contains(normalizedQuery) ||
                x.User.FullName.Contains(normalizedQuery));
        }

        var profiles = await profilesQuery
            .OrderByDescending(x => x.AverageRating)
            .ThenByDescending(x => x.ReviewCount)
            .Take(limit)
            .Select(x => new FreelancerSummary(
                x.Id,
                x.UserId,
                x.User.FullName,
                x.Tagline,
                x.Bio,
                x.CategoryId,
                x.ExperienceLevel,
                x.Availability,
                x.AverageRating,
                x.ReviewCount))
            .ToListAsync(ct);

        return await BuildListDtosAsync(profiles, ct);
    }

    public async Task<InternalFreelancerProfileDto?> GetFreelancerProfileAsync(Guid freelancerId, CancellationToken ct = default)
    {
        var profile = await _db.FreelancerProfiles.AsNoTracking()
            .Where(x => x.Id == freelancerId && x.IsActive)
            .Select(x => new
            {
                x.Id,
                x.UserId,
                x.User.FullName,
                x.Tagline,
                x.Bio,
                x.ExperienceLevel,
                x.Availability,
                x.IsPhoneVerified,
                x.AverageRating,
                x.ReviewCount
            })
            .FirstOrDefaultAsync(ct);
        if (profile is null) return null;

        var packages = await GetFreelancerPackagesAsync(freelancerId, ct);
        var portfolio = await _db.FreelancerPortfolios.AsNoTracking()
            .Where(x => x.UserId == profile.UserId)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new InternalPortfolioDto(x.Id, x.Title, x.Description, x.ImageUrl))
            .ToListAsync(ct);
        var skills = await GetSkillsAsync(profile.UserId, ct);

        return new InternalFreelancerProfileDto(
            profile.Id,
            profile.FullName,
            profile.Tagline,
            profile.Bio,
            profile.ExperienceLevel,
            profile.Availability,
            profile.IsPhoneVerified,
            profile.AverageRating,
            profile.ReviewCount,
            skills,
            packages,
            portfolio);
    }

    public async Task<IReadOnlyList<InternalReviewDto>> GetFreelancerReviewsAsync(Guid freelancerId, CancellationToken ct = default)
    {
        var userId = await _db.FreelancerProfiles.AsNoTracking()
            .Where(x => x.Id == freelancerId)
            .Select(x => (Guid?)x.UserId)
            .FirstOrDefaultAsync(ct);
        if (!userId.HasValue) return [];

        return await _db.Reviews.AsNoTracking()
            .Where(x => x.RevieweeId == userId.Value)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new InternalReviewDto(x.Id, x.ContractId, x.ReviewerId, x.Rating, x.Comment, x.CreatedAt))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<InternalPackageDto>> GetFreelancerPackagesAsync(Guid freelancerId, CancellationToken ct = default)
    {
        var userId = await _db.FreelancerProfiles.AsNoTracking()
            .Where(x => x.Id == freelancerId)
            .Select(x => (Guid?)x.UserId)
            .FirstOrDefaultAsync(ct);
        if (!userId.HasValue) return [];

        return await _db.FreelancerPackages.AsNoTracking()
            .Where(x => x.UserId == userId.Value)
            .OrderBy(x => x.Price)
            .Select(x => new InternalPackageDto(x.Id, x.PackageType, x.Price, x.DeliveryDays, x.Description))
            .ToListAsync(ct);
    }

    public async Task<InternalCommissionDto> GetCommissionAsync(CancellationToken ct = default)
    {
        var commission = await _db.GlobalSettings.AsNoTracking()
            .Where(x => x.Id == 1)
            .Select(x => (decimal?)x.CommissionPercentage)
            .FirstOrDefaultAsync(ct) ?? 10.00m;
        return new InternalCommissionDto(commission, "USD");
    }

    public async Task<IReadOnlyList<InternalDisputeDto>> GetContractDisputesAsync(int contractId, CancellationToken ct = default)
    {
        return await _db.Disputes.AsNoTracking()
            .Where(x => x.ContractId == contractId)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new InternalDisputeDto(
                x.Id,
                x.ContractId,
                x.Reason.ToString(),
                x.Description,
                x.Status.ToString(),
                x.Resolution.HasValue ? x.Resolution.Value.ToString() : null,
                x.CreatedAt))
            .ToListAsync(ct);
    }

    public async Task<InternalBriefDto?> GetBriefAsync(int briefId, CancellationToken ct = default)
    {
        return await _db.Briefs.AsNoTracking()
            .Where(x => x.Id == briefId)
            .Select(x => new InternalBriefDto(
                x.Id,
                x.Text,
                x.BudgetHint,
                x.DeadlineHint,
                x.Status.ToString(),
                x.Analysis == null
                    ? null
                    : new InternalBriefAnalysisDto(
                        x.Analysis.Id,
                        x.Analysis.RequiredSkills,
                        x.Analysis.Complexity,
                        x.Analysis.SuggestedBudget,
                        x.Analysis.Timeline,
                        x.Analysis.Confidence)))
            .FirstOrDefaultAsync(ct);
    }

    private async Task<IReadOnlyList<InternalFreelancerListDto>> BuildListDtosAsync(IReadOnlyList<FreelancerSummary> profiles, CancellationToken ct)
    {
        var userIds = profiles.Select(x => x.UserId).ToArray();
        var skills = await (from fs in _db.FreelancerSkills.AsNoTracking()
                            join skill in _db.Skills.AsNoTracking() on fs.SkillId equals skill.Id into skillJoin
                            from skill in skillJoin.DefaultIfEmpty()
                            where userIds.Contains(fs.UserId)
                            select new { fs.UserId, fs.SkillId, fs.CustomName, SkillName = skill == null ? null : skill.Name })
            .ToListAsync(ct);
        var packages = await _db.FreelancerPackages.AsNoTracking()
            .Where(x => userIds.Contains(x.UserId))
            .Select(x => new { x.UserId, Package = new InternalPackageDto(x.Id, x.PackageType, x.Price, x.DeliveryDays, x.Description) })
            .ToListAsync(ct);

        return profiles.Select(x => new InternalFreelancerListDto(
            x.Id,
            x.FullName,
            x.Tagline,
            x.Bio,
            x.CategoryId,
            x.ExperienceLevel,
            x.Availability,
            x.AverageRating,
            x.ReviewCount,
            skills.Where(s => s.UserId == x.UserId).Select(s => !string.IsNullOrWhiteSpace(s.CustomName) ? s.CustomName : s.SkillName ?? $"skill:{s.SkillId}").ToList(),
            packages.Where(p => p.UserId == x.UserId).Select(p => p.Package).ToList())).ToList();
    }

    private sealed record FreelancerSummary(
        Guid Id,
        Guid UserId,
        string FullName,
        string Tagline,
        string Bio,
        int? CategoryId,
        string ExperienceLevel,
        bool Availability,
        decimal AverageRating,
        int ReviewCount);

    private async Task<IReadOnlyList<string>> GetSkillsAsync(Guid userId, CancellationToken ct)
        => await _db.FreelancerSkills.AsNoTracking()
            .Where(x => x.UserId == userId)
            .Select(x => new { x.SkillId, x.CustomName })
            .Select(x => string.IsNullOrWhiteSpace(x.CustomName) ? $"skill:{x.SkillId}" : x.CustomName)
            .ToListAsync(ct);
}
