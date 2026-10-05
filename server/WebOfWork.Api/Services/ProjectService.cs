using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using WebOfWork.Api.Data;
using WebOfWork.Api.DTOs.Projects;
using WebOfWork.Api.Models;
using WebOfWork.Api.Models.Enums;

namespace WebOfWork.Api.Services;

public interface IProjectService
{
    Task<ProjectResponse> CreateProjectAsync(Guid userId, CreateProjectRequest request);
    Task<ProjectListResponse> GetPagedProjectsAsync(string? search = null, int? categoryId = null, int? skillId = null, decimal? minPrice = null, decimal? maxPrice = null, int? deliveryDays = null, int page = 1, int pageSize = 20);
    /// <summary>
    /// The reference ProjectListView scopes to <c>Project.objects.filter(client=user)</c>, i.e. the
    /// caller's own posts. The open-project browse feed lives on
    /// <see cref="GetPagedProjectsAsync"/> and the freelancer route separately.
    /// </summary>
    Task<ProjectListResponse> GetPagedMyProjectsAsync(Guid userId, string? search = null, ProjectStatus? status = null, int page = 1, int pageSize = 20);
    Task<ProjectResponse?> GetProjectDetailAsync(int projectId);
    Task<ProjectResponse> UpdateProjectAsync(Guid userId, int projectId, UpdateProjectRequest request);
    Task<bool> DeleteProjectAsync(Guid userId, int projectId);
    Task<List<ProjectResponse>> GetMyProjectsAsync(Guid userId);
    Task<ProjectResponse?> GetMyProjectDetailAsync(Guid userId, int projectId);
}

public class ProjectService : IProjectService
{
    private readonly WebOfWorkDbContext _db;
    private readonly ICurrentUser? _currentUser;

    public ProjectService(WebOfWorkDbContext db, ICurrentUser? currentUser = null)
    {
        _db = db;
        _currentUser = currentUser;
    }

    private async Task<Guid> GetClientIdAsync(Guid userId)
    {
        var profile = await _db.ClientProfiles.FirstOrDefaultAsync(c => c.UserId == userId);
        if (profile == null) throw new UnauthorizedAccessException("Client profile not found.");
        return profile.Id;
    }

    public async Task<ProjectResponse> CreateProjectAsync(Guid userId, CreateProjectRequest request)
    {
            var categoryId = request.Category ?? request.CategoryId;
            if (categoryId is null) throw new ArgumentException("Category is required");
            var minBudget = request.MinBudget ?? request.MinPrice;
            var maxBudget = request.MaxBudget ?? request.MaxPrice;
             // The React form posts `pricing_type` ("fixed"/"range"); PricingTypeName is the legacy alias.
 // Prefer the new field -- reading only PricingTypeName discarded the form's choice, so
 // every project silently defaulted to Fixed regardless of the selected pricing type.
 var pricingRaw = request.PricingType ?? request.PricingTypeName;
 var pricing = string.Equals(pricingRaw, "range", StringComparison.OrdinalIgnoreCase) ? ProjectPricingType.Range : ProjectPricingType.Fixed;
            if (pricing == ProjectPricingType.Fixed && request.FixedPrice is null) throw new ArgumentException("Fixed price is required for fixed pricing.");
            if (pricing == ProjectPricingType.Range && (minBudget is null || maxBudget is null || minBudget >= maxBudget)) throw new ArgumentException("Both min and max budget are required and min must be less than max.");
            var clientId = await GetClientIdAsync(userId);
            var project = new Project { ClientId = clientId, CategoryId = categoryId, Title = request.Title, Description = request.Description, Requirements = request.Requirements, ExpectedDeliverables = request.ExpectedDeliverables, PricingType = pricing, FixedPrice = pricing == ProjectPricingType.Fixed ? request.FixedPrice : null, MinPrice = pricing == ProjectPricingType.Range ? minBudget : null, MaxPrice = pricing == ProjectPricingType.Range ? maxBudget : null, ExpectedDays = request.DeliveryDays > 0 ? request.DeliveryDays : request.ExpectedDays, Status = ProjectStatus.Open, IsActive = true };
            foreach (var raw in request.Skills)
            {
                // Trim and reject blanks BEFORE indexing.
                //
                // This read `raw.Trim()[0]` and only afterwards checked
                // `name.Length == 0`, so the guard could never run: building
                // `name` from an empty string is what throws. `["  ", "  ", "  "]`
                // passes the client's schema -- both old regexes allow whitespace
                // -- and reaches this line as a 500 rather than a skipped entry.
                var trimmed = raw?.Trim();
                if (string.IsNullOrEmpty(trimmed)) continue;

                // Matched case-insensitively on both sides, so no normalisation is needed
                // for the lookup -- and normalising was actively harmful.
                //
                // This used to title-case: `char.ToUpperInvariant(name[0]) +
                // name[1..].ToLowerInvariant()`. That lowercased everything after
                // the first character, so a custom skill was stored mangled:
                // "Underwater Basket Weaving" became "Underwater basket weaving",
                // "n8n" became "N8n", "macOS" became "Macos". The client's own
                // suggestion chips and its AI-filled badge both promise the name
                // as shown, and the name is what a freelancer reads when deciding
                // whether to apply.
                //
                // Trimmed, and otherwise stored exactly as the client typed it.
                var name = trimmed;
                var skill = await _db.Skills.FirstOrDefaultAsync(x => x.Name.ToLower() == name.ToLower());
                project.Skills.Add(new ProjectSkill { SkillId = skill?.Id, CustomName = skill == null ? name : null });
            }
            _db.Projects.Add(project); await _db.SaveChangesAsync(); return await GetProjectDetailAsync(project.Id) ?? MapToResponse(project);
    }

    public async Task<ProjectListResponse> GetPagedProjectsAsync(string? search = null, int? categoryId = null, int? skillId = null, decimal? minPrice = null, decimal? maxPrice = null, int? deliveryDays = null, int page = 1, int pageSize = 20)
    {
        var query = BuildOpenProjectQuery(search, categoryId, skillId, minPrice, maxPrice, deliveryDays);
        var normalizedPage = Math.Max(page, 1);
        var normalizedPageSize = Math.Clamp(pageSize, 1, 100);
        var count = await query.CountAsync();
        var items = await query.OrderByDescending(p => p.CreatedAt).Skip((normalizedPage - 1) * normalizedPageSize).Take(normalizedPageSize).ToListAsync();

        return new ProjectListResponse
        {
            Results = items.Select(MapToResponse).ToList(),
            Count = count,
            Page = normalizedPage,
            PageSize = normalizedPageSize,
            Counts = new ProjectCountResponse
            {
                All = await _db.Projects.CountAsync(p => p.IsActive),
                Completed = await _db.Projects.CountAsync(p => p.IsActive && p.Status == ProjectStatus.Completed),
                InProgress = await _db.Projects.CountAsync(p => p.IsActive && p.Status == ProjectStatus.InProgress),
                Open = await _db.Projects.CountAsync(p => p.IsActive && p.Status == ProjectStatus.Open)
            }
        };
    }

    public async Task<ProjectListResponse> GetPagedMyProjectsAsync(Guid userId, string? search = null, ProjectStatus? status = null, int page = 1, int pageSize = 20)
    {
        var normalizedPage = Math.Max(page, 1);
        var normalizedPageSize = Math.Clamp(pageSize, 1, 100);

        // projects.client_id references ClientProfiles.Id, not Users.Id.
        var clientId = await GetClientIdAsync(userId);
        var mine = _db.Projects.Where(p => p.ClientId == clientId);
        var query = mine;
        if (status.HasValue) query = query.Where(p => p.Status == status.Value);

        // A client's own project list is a management screen, not a search result
        // page: newest-first is what they expect and what the tab counts assume.
        // So relevance ranking is deliberately NOT applied here, only the matching.
        // `rank: false` makes `Apply` order by `CreatedAt` itself.
        var filtered = query.Apply(search, titleStartsWith: true);

        var count = await filtered.CountAsync();
        var items = await filtered.Skip((normalizedPage - 1) * normalizedPageSize)
            .Take(normalizedPageSize)
            .ToListAsync();

        return new ProjectListResponse
        {
            Results = items.Select(MapToResponse).ToList(),
            Count = count,
            Page = normalizedPage,
            PageSize = normalizedPageSize,
            // Counts must be per-client too, otherwise the tab badges disagree with the rows.
            Counts = new ProjectCountResponse
            {
                All = await mine.CountAsync(p => p.Status != ProjectStatus.Cancelled),
                Completed = await mine.CountAsync(p => p.Status == ProjectStatus.Completed),
                InProgress = await mine.CountAsync(p => p.Status == ProjectStatus.InProgress),
                Open = await mine.CountAsync(p => p.Status == ProjectStatus.Open)
            }
        };
    }

    public async Task<List<ProjectResponse>> GetProjectsAsync(string? search = null, int? categoryId = null, int? skillId = null, decimal? minPrice = null, decimal? maxPrice = null, int? deliveryDays = null)
    {
        // `BuildOpenProjectQuery` already ranked by relevance and added newest-first
        // as a tie-break, so no ordering is applied here. Adding one would discard
        // the ranking -- LINQ ordering is last-one-wins.
        var projects = await BuildOpenProjectQuery(search, categoryId, skillId, minPrice, maxPrice, deliveryDays)
            .ToListAsync();

        return projects.Select(MapToResponse).ToList();
    }

    private IQueryable<Project> BuildOpenProjectQuery(string? search, int? categoryId, int? skillId, decimal? minPrice, decimal? maxPrice, int? deliveryDays)
    {
        var query = _db.Projects.Include(p => p.Skills).ThenInclude(ps => ps.Skill).Where(p => p.Status == ProjectStatus.Open && p.IsActive).AsQueryable();
        if (categoryId.HasValue) query = query.Where(p => p.CategoryId == categoryId.Value);
        if (skillId.HasValue) query = query.Where(p => p.Skills.Any(s => s.SkillId == skillId.Value));
        if (minPrice.HasValue) query = query.Where(p => (p.FixedPrice ?? p.MinPrice ?? 0m) >= minPrice.Value);
        if (maxPrice.HasValue) query = query.Where(p => (p.FixedPrice ?? p.MaxPrice ?? decimal.MaxValue) <= maxPrice.Value);
        if (deliveryDays.HasValue) query = query.Where(p => p.ExpectedDays <= deliveryDays.Value);

        // Full-text, not `Contains`. See ProjectSearch for why: `ILIKE '%term%'`
        // cannot use an index and matches substrings, so "react" found "Reactor".
        //
        // Applied last, and with the ordering here rather than at the call site,
        // because a caller that adds its own `OrderBy` afterwards would silently
        // discard the relevance ranking -- LINQ ordering is last-one-wins.
        // Full-text, not `Contains`. See ProjectSearch for why: `ILIKE '%term%'`
        // cannot use an index and matches substrings, so "react" found "Reactor".
        //
        // `Apply` owns the ordering, including newest-first when there is no search
        // term, so no `OrderBy` is added here. Adding one would silently discard the
        // relevance ranking -- LINQ ordering is last-one-wins.
        return query.Apply(search, rank: true, titleStartsWith: true)
            .ThenByDescending(p => p.CreatedAt);
    }

    public async Task<ProjectResponse?> GetProjectDetailAsync(int projectId)
    {
        var project = await _db.Projects.Include(p => p.Client).ThenInclude(c => c.User).Include(p => p.Skills).ThenInclude(ps => ps.Skill).FirstOrDefaultAsync(p => p.Id == projectId);
        if (project == null) return null;
        var response = MapToResponse(project);
        response.ClientInfo = new { project.Client.User.FullName, project.Client.User.Email };
        response.ProposalsCount = await _db.Proposals.CountAsync(p => p.ProjectId == projectId);
        response.ContractId = await _db.Contracts.Where(c => c.ProjectId == projectId).Select(c => (int?)c.Id).FirstOrDefaultAsync();
        response.WorkflowRunId = await _db.WorkflowRuns.Where(w => w.ProjectId == projectId).OrderByDescending(w => w.CreatedAt).Select(w => (int?)w.Id).FirstOrDefaultAsync();
        if (_currentUser?.Id is Guid viewerId)
        {
            var freelancerId = await _db.FreelancerProfiles.Where(f => f.UserId == viewerId).Select(f => (Guid?)f.Id).FirstOrDefaultAsync();
            if (freelancerId.HasValue)
                response.IsApplied = await _db.Proposals.AnyAsync(p => p.ProjectId == projectId && p.FreelancerId == freelancerId.Value && p.Status != ProposalStatus.Withdrawn);
        }
        return response;
    }

    public async Task<ProjectResponse> UpdateProjectAsync(Guid userId, int projectId, UpdateProjectRequest request)
    {
        var clientId = await GetClientIdAsync(userId);

        var project = await _db.Projects.Include(p => p.Skills).FirstOrDefaultAsync(p => p.Id == projectId);

        if (project == null) throw new KeyNotFoundException("Project not found.");
        if (project.ClientId != clientId) throw new UnauthorizedAccessException("You do not own this project.");

        project.Title = request.Title;
        project.Description = request.Description;
        project.Requirements = request.Requirements;
        project.ExpectedDeliverables = request.ExpectedDeliverables;
        // EditProject.jsx sends delivery_days; ExpectedDays is the legacy fallback.
        if (request.DeliveryDays > 0) project.ExpectedDays = request.DeliveryDays;
        else if (request.ExpectedDays > 0) project.ExpectedDays = request.ExpectedDays;
        project.CategoryId = request.CategoryId ?? project.CategoryId;
        project.UpdatedAt = DateTime.UtcNow;

        // Pricing. EditProject.jsx has always submitted these; before this the values
        // were dropped, so saving a new budget returned 200 and changed nothing.
        // Null means "not supplied", keeping partial updates working. Switching
        // pricing type clears the other shape, otherwise a project would keep a
        // stale fixed_price *and* a min/max range.
        var updMin = request.MinBudget ?? request.MinPrice;
        var updMax = request.MaxBudget ?? request.MaxPrice;
        if (request.PricingType is not null)
        {
            project.PricingType = string.Equals(request.PricingType, "range",
                StringComparison.OrdinalIgnoreCase)
                ? ProjectPricingType.Range
                : ProjectPricingType.Fixed;
        }

        if (project.PricingType == ProjectPricingType.Fixed)
        {
            if (request.FixedPrice is not null) project.FixedPrice = request.FixedPrice;
            project.MinPrice = null;
            project.MaxPrice = null;
        }
        else
        {
            if (updMin is not null) project.MinPrice = updMin;
            if (updMax is not null) project.MaxPrice = updMax;
            project.FixedPrice = null;
        }

        if (project.PricingType == ProjectPricingType.Range
            && project.MinPrice is not null && project.MaxPrice is not null
            && project.MinPrice >= project.MaxPrice)
            throw new ArgumentException("Max budget must be greater than min budget.");

        // Replace skill set atomically
        _db.ProjectSkills.RemoveRange(project.Skills);
        project.Skills.Clear();

        if (request.SkillIds != null)
            foreach (var sid in request.SkillIds)
                project.Skills.Add(new ProjectSkill { SkillId = sid, ProjectId = project.Id });

        if (request.CustomSkillNames != null)
            foreach (var name in request.CustomSkillNames.Where(n => !string.IsNullOrWhiteSpace(n)))
                project.Skills.Add(new ProjectSkill { CustomName = name, ProjectId = project.Id });

        await _db.SaveChangesAsync();
        return await GetProjectDetailAsync(project.Id) ?? MapToResponse(project);
    }

    public async Task<bool> DeleteProjectAsync(Guid userId, int projectId)
    {
        var clientId = await GetClientIdAsync(userId);

        var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == projectId);
        if (project == null) return false;
        if (project.ClientId != clientId) throw new UnauthorizedAccessException("You do not own this project.");

        _db.Projects.Remove(project);
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<List<ProjectResponse>> GetMyProjectsAsync(Guid userId)
    {
        var clientId = await GetClientIdAsync(userId);

        var projects = await _db.Projects.Include(p => p.Skills).ThenInclude(ps => ps.Skill).Where(p => p.ClientId == clientId).OrderByDescending(p => p.CreatedAt).ToListAsync();

        return projects.Select(MapToResponse).ToList();
    }

    public async Task<ProjectResponse?> GetMyProjectDetailAsync(Guid userId, int projectId)
    {
        var clientId = await GetClientIdAsync(userId);

        var project = await _db.Projects.Include(p => p.Skills).ThenInclude(ps => ps.Skill).FirstOrDefaultAsync(p => p.Id == projectId);

        if (project == null) return null;
        if (project.ClientId != clientId) throw new UnauthorizedAccessException("You do not own this project.");

        return MapToResponse(project);
    }

    private static ProjectResponse MapToResponse(Project project) => new()
    {
        Id = project.Id,
        ClientId = project.ClientId,
        CategoryId = project.CategoryId,
        CategoryName = project.Category?.Name ?? string.Empty,
        Category = project.CategoryId,
        Title = project.Title,
        Description = project.Description,
        Requirements = project.Requirements,
        ExpectedDeliverables = project.ExpectedDeliverables,
        PricingType = project.PricingType,
        FixedPrice = project.FixedPrice,
        MinBudget = project.MinPrice,
        MaxBudget = project.MaxPrice,
        DeliveryDays = project.ExpectedDays,
        Status = project.Status,
        IsActive = project.IsActive,
        CreatedAt = project.CreatedAt,
        UpdatedAt = project.UpdatedAt,
        // The reference get_skills returns names; JobCard renders each entry as a string.
        Skills = project.Skills.Select(ps => ps.Skill?.Name ?? ps.CustomName ?? string.Empty).ToList()
    };
}
