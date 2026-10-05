using Microsoft.EntityFrameworkCore;
using WebOfWork.Api.Data;
using WebOfWork.Api.DTOs.Internal;
using WebOfWork.Api.Models;
using WebOfWork.Api.Models.Enums;

namespace WebOfWork.Api.Services;

public interface IInternalReadService
{
    Task<InternalProjectDto?> GetProjectAsync(int projectId, CancellationToken ct = default);
    Task<IReadOnlyList<InternalSimilarProjectDto>> GetSimilarProjectsAsync(int categoryId, int limit = 10, int? excludeProjectId = null, CancellationToken ct = default);
    Task<IReadOnlyList<InternalFreelancerListDto>> SearchFreelancersAsync(string? query, int? categoryId, decimal? minRating, bool? available, int limit = 50, CancellationToken ct = default);
    Task<InternalFreelancerProfileDto?> GetFreelancerProfileAsync(Guid freelancerId, CancellationToken ct = default);
    Task<IReadOnlyList<InternalReviewDto>> GetFreelancerReviewsAsync(Guid freelancerId, CancellationToken ct = default);
    Task<IReadOnlyList<InternalPackageDto>> GetFreelancerPackagesAsync(Guid freelancerId, CancellationToken ct = default);
    Task<InternalCommissionDto> GetCommissionAsync(CancellationToken ct = default);
    Task<IReadOnlyList<InternalPriceBandDto>> GetPriceBandsAsync(int? categoryId, string? search, CancellationToken ct = default);
    Task<IReadOnlyList<InternalSkillDto>> GetCategorySkillsAsync(int categoryId, string? search, CancellationToken ct = default);
    Task<IReadOnlyList<InternalDisputeDto>> GetContractDisputesAsync(int contractId, CancellationToken ct = default);
    Task<InternalBriefDto?> GetBriefAsync(int briefId, CancellationToken ct = default);
}

public sealed class InternalReadService : IInternalReadService
{
    /// <summary>
    /// The largest pool the matching agent asks to score.
    /// </summary>
    /// <remarks>
    /// The agent passes <c>limit=200</c> so it can rank the whole pool rather than
    /// an arbitrary prefix of it. This is the ceiling that request is clamped to,
    /// and it must not be lowered below it: the clamp is invisible to the caller,
    /// so lowering it silently changes which candidates were considered.
    /// </remarks>
    public const int MaxPoolSize = 200;

    private readonly WebOfWorkDbContext _db;

    public InternalReadService(WebOfWorkDbContext db)
    {
        _db = db;
    }

    public async Task<InternalProjectDto?> GetProjectAsync(int projectId, CancellationToken ct = default)
    {
        // `Skills` and `Client` are loaded rather than filtered out, because the
        // agent service needs the project's required skills to score candidates
        // and has no other endpoint that returns them with the project.
        var project = await _db.Projects.AsNoTracking()
            .Include(x => x.Skills).ThenInclude(s => s.Skill)
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
            project.ClientId,
            project.CategoryId,
            // `CustomName` first: a project can list a skill the catalogue does
            // not have, and dropping those would understate what the work needs.
            project.Skills
                .Select(s => s.Skill?.Name ?? s.CustomName)
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Select(n => n!)
                .ToList());
    }

    /// <summary>
      /// Recently posted projects in one category, newest first.
      /// </summary>
      /// <remarks>
      /// Backs the planner's `get_similar_projects` tool, which it advertises as
      /// the way to judge what a normal budget and delivery window look like.
      ///
      /// That tool used to return an empty list unconditionally. It was the one
      /// tool in the subsystem that actively misled the model rather than merely
      /// doing nothing: the planner was told to sanity-check budget expectations
      /// against comparables, called the tool, got `[]`, and could reasonably
      /// conclude the category had no comparable history — which is a statement
      /// about the business, invented by a stub. The advertised-vs-permitted test
      /// could not catch it, because the tool genuinely was permitted; permission
      /// parity says nothing about whether a tool works.
      ///
      /// Three deliberate choices:
      ///
      /// <list type="bullet">
      /// <item>Newest first, and capped, because "what does a normal budget look
      /// like" is a question about the current market rather than about the first
      /// projects ever posted in the category.</item>
      /// <item>Only <c>Open</c> and <c>InProgress</c>. A completed or cancelled
      /// project's budget is a historical figure that nobody is now competing
      /// against, and mixing them in skews the comparison the tool exists to
      /// support.</item>
      /// <item><c>excludeProjectId</c> exists so the caller can leave the project
      /// being planned out of its own baseline. Without it the project under
      /// planning is one of the comparables, which quietly biases the very
      /// judgement the tool is being consulted for.</item>
      /// </list>
      /// </remarks>
      public async Task<IReadOnlyList<InternalSimilarProjectDto>> GetSimilarProjectsAsync(
          int categoryId,
          int limit = 10,
          int? excludeProjectId = null,
          CancellationToken ct = default)
      {
          // Bounded by the caller as well as here: a model that passes 100000
          // should get a sane page, not the table.
          var take = Math.Clamp(limit, 1, 50);

          var query = _db.Projects.AsNoTracking()
              .Where(p => p.CategoryId == categoryId
                       && (p.Status == ProjectStatus.Open || p.Status == ProjectStatus.InProgress));

          if (excludeProjectId is int exclude)
              query = query.Where(p => p.Id != exclude);

          return await query
              .OrderByDescending(p => p.CreatedAt)
              .Take(take)
              .Select(p => new InternalSimilarProjectDto(
                  p.Id,
                  p.Title,
                  p.PricingType.ToString(),
                  p.FixedPrice,
                  p.MinPrice,
                  p.MaxPrice,
                  p.ExpectedDays,
                  p.Status.ToString(),
                  p.CreatedAt))
              .ToListAsync(ct);
      }

      public async Task<IReadOnlyList<InternalFreelancerListDto>> SearchFreelancersAsync(
        string? query,
        int? categoryId,
        decimal? minRating,
        bool? available,
        int limit = 50,
        CancellationToken ct = default)
    {
        // Clamped, but at the ceiling the caller actually asks for.
        //
        // It was `Math.Clamp(limit, 1, 50)`. The matching agent requests 200 --
        // deliberately, to see the whole pool before ranking it -- and was silently
        // given 50. A shortlist built from the top 50 by rating is a different
        // answer to a different question, and nothing said so: the response is a
        // well-formed list, just not the one that was requested.
        //
        // The clamp still exists, because this is an unbounded query over a public
        // table and the ceiling is now the largest number the only caller asks for.
        limit = Math.Clamp(limit, 1, MaxPoolSize);
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

    /// <summary>
    /// The reference price bands, optionally narrowed to one category.
    /// </summary>
    /// <remarks>
    /// A null <paramref name="categoryId"/> returns every row, which is a few
    /// hundred. That is deliberate: the agent matches the deliverable by wording,
    /// so it needs to see the whole vocabulary to know what exists before it can
    /// judge relevance. Narrowing is an optimisation for a caller that already
    /// knows the category, not the normal path.
    ///
    /// Rows with a null category are included either way. They are the
    /// cross-cutting bands, and filtering them out would make them unreachable
    /// for the one caller that could have used them.
    /// </remarks>
    /// <summary>
    /// The real skills in one category, for the drafting agent to ground against.
    /// </summary>
    /// <remarks>
    /// This exists because `list_skills_by_category` had no data source at all.
    /// It filtered the injected category list on the id and returned the category
    /// row, so it answered "here is a category" to a question about skills -- and
    /// answered `[]` once the caller started sending categories without nesting
    /// their skills. A live draft had the model call it seven times in a row,
    /// once per category, before giving up on skills entirely.
    ///
    /// Scoped to one category on purpose. The whole catalogue is 654 skills, which
    /// overflowed the provider's request limit (two 413s) when nested into the
    /// categories payload. One category is small enough to choose from.
    ///
    /// `category_id` is returned on every row, not just implied by the route: the
    /// agent's grounding has to be able to tell a skill row from a category row,
    /// and both are otherwise just `{id, name}`.
    /// </remarks>
    public async Task<IReadOnlyList<InternalSkillDto>> GetCategorySkillsAsync(
        int categoryId, string? search, CancellationToken ct = default)
    {
        var query = _db.Skills.AsNoTracking().Where(x => x.CategoryId == categoryId);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(x => x.Name.ToLower().Contains(term));
        }

        return await query
            .OrderBy(x => x.Name)
            .Select(x => new InternalSkillDto(x.Id, x.Name, x.CategoryId, x.IsCustom))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<InternalPriceBandDto>> GetPriceBandsAsync(
        int? categoryId, string? search, CancellationToken ct = default)
    {
        var query = _db.MarketPriceReferences.AsNoTracking()
            .AsQueryable();

        if (categoryId.HasValue)
        {
            var id = categoryId.Value;
            query = query.Where(x => x.CategoryId == null || x.CategoryId == id);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(x => x.Deliverable.ToLower().Contains(term));
        }

        return await query
            .OrderBy(x => x.Deliverable)
            .ThenBy(x => x.MinPriceUsd)
            .Select(x => new InternalPriceBandDto(
                x.Deliverable,
                x.Level,
                x.MinPriceUsd,
                x.MaxPriceUsd,
                x.TypicalDaysMin,
                x.TypicalDaysMax,
                x.CategoryId,
                x.Category == null ? null : x.Category.Name,
                x.Source))
            .ToListAsync(ct);
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

        // Portfolio, for the same reason the detail endpoint includes it: the
        // matching agent scores `portfolio_relevance` as term overlap against this
        // text, and reads the pool from this endpoint alone.
        var portfolio = await _db.FreelancerPortfolios.AsNoTracking()
            .Where(x => userIds.Contains(x.UserId))
            .Select(x => new
            {
                x.UserId,
                Entry = new InternalPortfolioDto(x.Id, x.Title, x.Description, x.ImageUrl),
            })
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
            packages.Where(p => p.UserId == x.UserId).Select(p => p.Package).ToList(),
            portfolio.Where(p => p.UserId == x.UserId).Select(p => p.Entry).ToList())).ToList();
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
