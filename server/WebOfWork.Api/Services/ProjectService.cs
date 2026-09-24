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
        var clientId = await GetClientIdAsync(userId);

        var project = new Project
        {
            ClientId = clientId,
            CategoryId = request.CategoryId,
            Title = request.Title,
            Description = request.Description,
            Requirements = request.Requirements,
            ExpectedDeliverables = request.ExpectedDeliverables,
            PricingType = (ProjectPricingType)request.PricingType,
            FixedPrice = request.FixedPrice,
            MinPrice = request.MinPrice,
            MaxPrice = request.MaxPrice,
            ExpectedDays = request.ExpectedDays,
            Status = ProjectStatus.Open,
            IsActive = true
        };

        // FK-backed skills
        if (request.SkillIds != null)
            foreach (var sid in request.SkillIds)
                project.Skills.Add(new ProjectSkill { SkillId = sid });

        // Custom-name skills (mirrors ref: custom_name field on ProjectSkill)
        if (request.CustomSkillNames != null)
            foreach (var name in request.CustomSkillNames.Where(n => !string.IsNullOrWhiteSpace(n)))
                project.Skills.Add(new ProjectSkill { CustomName = name });

        _db.Projects.Add(project);
        await _db.SaveChangesAsync();

        return await GetProjectDetailAsync(project.Id) ?? MapToResponse(project);
    }

    public async Task<ProjectListResponse> GetPagedProjectsAsync(string? search = null, int? categoryId = null, int? skillId = null, decimal? minPrice = null, decimal? maxPrice = null, int? deliveryDays = null, int page = 1, int pageSize = 20)
    {
        var query = BuildOpenProjectQuery(search, categoryId, skillId, minPrice, maxPrice, deliveryDays);
        var normalizedPage = Math.Max(page, 1);
        var normalizedPageSize = Math.Clamp(pageSize, 1, 100);
        var count = await query.CountAsync();
        var items = await query.OrderByDescending(p => p.CreatedAt)
            .Skip((normalizedPage - 1) * normalizedPageSize)
            .Take(normalizedPageSize)
            .ToListAsync();

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

    public async Task<List<ProjectResponse>> GetProjectsAsync(string? search = null, int? categoryId = null, int? skillId = null, decimal? minPrice = null, decimal? maxPrice = null, int? deliveryDays = null)
    {
        var projects = await BuildOpenProjectQuery(search, categoryId, skillId, minPrice, maxPrice, deliveryDays)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();
        return projects.Select(MapToResponse).ToList();
    }

    private IQueryable<Project> BuildOpenProjectQuery(string? search, int? categoryId, int? skillId, decimal? minPrice, decimal? maxPrice, int? deliveryDays)
    {
        var query = _db.Projects
            .Include(p => p.Skills).ThenInclude(ps => ps.Skill)
            .Where(p => p.Status == ProjectStatus.Open && p.IsActive)
            .AsQueryable();
        if (!string.IsNullOrWhiteSpace(search)) query = query.Where(p => p.Title.Contains(search) || p.Description.Contains(search));
        if (categoryId.HasValue) query = query.Where(p => p.CategoryId == categoryId.Value);
        if (skillId.HasValue) query = query.Where(p => p.Skills.Any(s => s.SkillId == skillId.Value));
        if (minPrice.HasValue) query = query.Where(p => (p.FixedPrice ?? p.MinPrice ?? 0m) >= minPrice.Value);
        if (maxPrice.HasValue) query = query.Where(p => (p.FixedPrice ?? p.MaxPrice ?? decimal.MaxValue) <= maxPrice.Value);
        if (deliveryDays.HasValue) query = query.Where(p => p.ExpectedDays <= deliveryDays.Value);
        return query;
    }

    public async Task<ProjectResponse?> GetProjectDetailAsync(int projectId)
    {
        var project = await _db.Projects
            .Include(p => p.Client).ThenInclude(c => c.User)
            .Include(p => p.Skills).ThenInclude(ps => ps.Skill)
            .FirstOrDefaultAsync(p => p.Id == projectId);
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

        var project = await _db.Projects
            .Include(p => p.Skills)
            .FirstOrDefaultAsync(p => p.Id == projectId);

        if (project == null) throw new KeyNotFoundException("Project not found.");
        if (project.ClientId != clientId) throw new UnauthorizedAccessException("You do not own this project.");

        project.Title = request.Title;
        project.Description = request.Description;
        project.Requirements = request.Requirements;
        project.ExpectedDeliverables = request.ExpectedDeliverables;
        project.ExpectedDays = request.ExpectedDays;
        project.CategoryId = request.CategoryId ?? project.CategoryId;
        project.UpdatedAt = DateTime.UtcNow;

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

        var projects = await _db.Projects
            .Include(p => p.Skills).ThenInclude(ps => ps.Skill)
            .Where(p => p.ClientId == clientId)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();

        return projects.Select(MapToResponse).ToList();
    }

    public async Task<ProjectResponse?> GetMyProjectDetailAsync(Guid userId, int projectId)
    {
        var clientId = await GetClientIdAsync(userId);

        var project = await _db.Projects
            .Include(p => p.Skills).ThenInclude(ps => ps.Skill)
            .FirstOrDefaultAsync(p => p.Id == projectId);

        if (project == null) return null;
        if (project.ClientId != clientId) throw new UnauthorizedAccessException("You do not own this project.");

        return MapToResponse(project);
    }

    private static ProjectResponse MapToResponse(Project project) => new()
    {
        Id = project.Id,
        ClientId = project.ClientId,
        CategoryId = project.CategoryId,
        Title = project.Title,
        Description = project.Description,
        Requirements = project.Requirements,
        ExpectedDeliverables = project.ExpectedDeliverables,
        PricingType = project.PricingType.ToString(),
        FixedPrice = project.FixedPrice,
        MinPrice = project.MinPrice,
        MaxPrice = project.MaxPrice,
        ExpectedDays = project.ExpectedDays,
        Status = project.Status.ToString(),
        IsActive = project.IsActive,
        CreatedAt = project.CreatedAt,
        UpdatedAt = project.UpdatedAt,
        Skills = project.Skills.Select(ps => new ProjectSkillResponse
        {
            SkillId = ps.SkillId,
            Name = ps.Skill?.Name ?? ps.CustomName ?? string.Empty
        }).ToList()
    };
}
