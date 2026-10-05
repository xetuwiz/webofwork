using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using WebOfWork.Api.Data;
using WebOfWork.Api.DTOs.Projects;
using WebOfWork.Api.Models.Enums;

namespace WebOfWork.Api.Services;

public interface IProjectRecommendationService
{
    Task<List<ProjectResponse>> GetRecommendationsAsync(Guid userId);
}

public class ProjectRecommendationService : IProjectRecommendationService
{
    private readonly WebOfWorkDbContext _db;

    public ProjectRecommendationService(WebOfWorkDbContext db)
    {
        _db = db;
    }

    public async Task<List<ProjectResponse>> GetRecommendationsAsync(Guid userId)
    {
        // 1. Load freelancer context: profile (for categoryId), skills, pro package price
        //    Mirrors ref: get_freelancer_context(user)
        var freelancerProfile = await _db.FreelancerProfiles
            .FirstOrDefaultAsync(f => f.UserId == userId);

        var freelancerSkills = await _db.FreelancerSkills
            .Where(f => f.UserId == userId)
            .ToListAsync();

        // Build skill name set (FK name OR custom name, lowercased) — mirrors ref skill_names set
        var skillNames = freelancerSkills
            .Select(s => (s.CustomName ?? (s.SkillId.HasValue ? null : null))?.ToLower())
            .Where(n => n != null)
            .ToHashSet();

        // Also resolve skill FK names
        var skillIds = freelancerSkills
            .Where(s => s.SkillId.HasValue)
            .Select(s => s.SkillId!.Value)
            .ToList();

        // Get pro package price to filter by budget — mirrors ref: pro_price
        decimal proPrice = 0;
        var proPkg = await _db.FreelancerPackages
            .Where(p => p.UserId == userId && p.PackageType == "pro")
            .FirstOrDefaultAsync();
        if (proPkg != null) proPrice = proPkg.Price;

        // 2. No skills registered at all → fallback to recent open+active projects
        if (!freelancerSkills.Any())
        {
            return await GetRecentOpenProjectsAsync();
        }

        // 3. Build base query: open, active, filtered by category (if available)
        //    Mirrors ref: Q(status="open", is_active=True, category=freelancer.category)
        var query = _db.Projects
            .Include(p => p.Skills).ThenInclude(ps => ps.Skill)
            .Where(p => p.Status == ProjectStatus.Open && p.IsActive);

        if (freelancerProfile?.CategoryId != null)
            query = query.Where(p => p.CategoryId == freelancerProfile.CategoryId || p.CategoryId == null);

        // 4. Budget filter — mirrors ref:
        //    Q(pricing_type="fixed", fixed_price__gte=pro_price) |
        //    Q(pricing_type="range", max_budget__gte=pro_price)
        if (proPrice > 0)
        {
            query = query.Where(p =>
                (p.PricingType == ProjectPricingType.Fixed   && p.FixedPrice >= proPrice) ||
                (p.PricingType == ProjectPricingType.Range   && p.MaxPrice   >= proPrice));
        }

        var candidates = await query
            .Include(p => p.Category)
            .OrderByDescending(p => p.CreatedAt).Take(100).ToListAsync();

        // 5. Skill intersection filter (in-memory, mirrors ref: project_skills & skill_names)
        var matched = candidates.Where(project =>
        {
            var projectSkillNames = project.Skills
                .Select(ps => ps.Skill?.Name ?? ps.CustomName ?? string.Empty)
                .Select(n => n.ToLower())
                .ToHashSet();

            var projectSkillIds = project.Skills
                .Where(ps => ps.SkillId.HasValue)
                .Select(ps => ps.SkillId!.Value)
                .ToHashSet();

            return projectSkillIds.Intersect(skillIds).Any()
                || projectSkillNames.Intersect(skillNames!).Any();
        }).Take(20).ToList();

        // 6. If category+budget+skill filters yield nothing → fallback to recent
        if (!matched.Any())
            return await GetRecentOpenProjectsAsync();

        return matched.Select(p => new ProjectResponse
        {
            Id                   = p.Id,
            ClientId             = p.ClientId,
            CategoryId           = p.CategoryId,
            CategoryName         = p.Category?.Name ?? string.Empty,
            Title                = p.Title,
            Description          = p.Description,
            Requirements         = p.Requirements,
            ExpectedDeliverables = p.ExpectedDeliverables,
            PricingType          = p.PricingType,
            FixedPrice           = p.FixedPrice,
            MinBudget             = p.MinPrice,
            MaxBudget             = p.MaxPrice,
            DeliveryDays         = p.ExpectedDays,
            Status               = p.Status,
            IsActive             = p.IsActive,
            CreatedAt            = p.CreatedAt,
            UpdatedAt            = p.UpdatedAt,
               Skills               = p.Skills.Select(ps => ps.Skill?.Name ?? ps.CustomName ?? string.Empty).ToList()
        }).ToList();
    }

    private async Task<List<ProjectResponse>> GetRecentOpenProjectsAsync()
    {
        var projects = await _db.Projects
            .Include(p => p.Category)
            .Include(p => p.Skills).ThenInclude(ps => ps.Skill)
            .Where(p => p.Status == ProjectStatus.Open && p.IsActive)
            .OrderByDescending(p => p.CreatedAt)
            .Take(20)
            .ToListAsync();

        return projects.Select(p => new ProjectResponse
        {
            Id                   = p.Id,
            ClientId             = p.ClientId,
            CategoryId           = p.CategoryId,
            CategoryName         = p.Category?.Name ?? string.Empty,
            Title                = p.Title,
            Description          = p.Description,
            Requirements         = p.Requirements,
            ExpectedDeliverables = p.ExpectedDeliverables,
            PricingType          = p.PricingType,
            FixedPrice           = p.FixedPrice,
            MinBudget             = p.MinPrice,
            MaxBudget             = p.MaxPrice,
            DeliveryDays         = p.ExpectedDays,
            Status               = p.Status,
            IsActive             = p.IsActive,
            CreatedAt            = p.CreatedAt,
            UpdatedAt            = p.UpdatedAt,
               Skills               = p.Skills.Select(ps => ps.Skill?.Name ?? ps.CustomName ?? string.Empty).ToList()
        }).ToList();
    }
}
