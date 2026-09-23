using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using WebOfWork.Api.Data;
using WebOfWork.Api.DTOs.Profiles;
using WebOfWork.Api.Models;

namespace WebOfWork.Api.Services;

public interface IProfileService
{
    Task<ClientProfileResponse> GetClientProfileAsync(Guid userId);
    Task UpdateClientProfileAsync(Guid userId, UpdateClientProfileRequest request);
    Task<FreelancerProfileResponse> GetFreelancerProfileAsync(Guid userId);
    Task UpdateFreelancerProfileAsync(Guid userId, UpdateFreelancerProfileRequest request);
    Task<List<FreelancerListItemResponse>> GetFreelancersListAsync();
    Task<PublicFreelancerProfileResponse> GetPublicFreelancerProfileAsync(Guid id);
    Task SwitchRoleAsync(Guid userId, string newRole);
}

public class ProfileService : IProfileService
{
    private readonly WebOfWorkDbContext _db;

    public ProfileService(WebOfWorkDbContext db)
    {
        _db = db;
    }

    public async Task<ClientProfileResponse> GetClientProfileAsync(Guid userId)
    {
        var profile = await _db.ClientProfiles
            .Include(c => c.User)
            .FirstOrDefaultAsync(c => c.UserId == userId);

        if (profile == null) throw new UnauthorizedAccessException("Profile not found");

        return new ClientProfileResponse(
            profile.Id,
            profile.User.FullName,
            profile.User.Email,
            profile.CompanyName,
            profile.Location,
            profile.CreatedAt,
            false,
            profile.ReviewCount,
            profile.AverageRating
        );
    }

    public async Task UpdateClientProfileAsync(Guid userId, UpdateClientProfileRequest request)
    {
        var profile = await _db.ClientProfiles
            .Include(c => c.User)
            .FirstOrDefaultAsync(c => c.UserId == userId);

        if (profile == null) throw new UnauthorizedAccessException("Profile not found");

        if (request.FullName != null) profile.User.FullName = request.FullName;
        if (request.CompanyName != null) profile.CompanyName = request.CompanyName;
        if (request.Location != null) profile.Location = request.Location;

        await _db.SaveChangesAsync();
    }

    public async Task<FreelancerProfileResponse> GetFreelancerProfileAsync(Guid userId)
    {
        var profile = await _db.FreelancerProfiles
            .Include(f => f.User)
            .FirstOrDefaultAsync(f => f.UserId == userId);

        if (profile == null) throw new UnauthorizedAccessException("Profile not found");

        var skills = await _db.FreelancerSkills.Where(s => s.UserId == userId)
            .Select(s => new ProfileSkillDto(s.Id, s.SkillId, s.CustomName)).ToListAsync();

        var packages = await _db.FreelancerPackages.Where(p => p.UserId == userId)
            .Select(p => new ProfilePackageDto(p.Id, p.PackageType, p.Price, p.DeliveryDays, p.Description)).ToListAsync();

        var portfolios = await _db.FreelancerPortfolios.Where(p => p.UserId == userId)
            .Select(p => new PortfolioDto(p.Id, p.Title, p.Description, p.ImageUrl, p.CreatedAt)).ToListAsync();

        return new FreelancerProfileResponse(
            profile.Id, profile.User.FullName, profile.User.Email, profile.Tagline, profile.Bio,
            profile.Phone, profile.IsPhoneVerified, profile.CategoryId, profile.ExperienceLevel,
            profile.Availability, profile.AverageRating, profile.ReviewCount, profile.CreatedAt,
            skills, packages, portfolios
        );
    }

    public async Task UpdateFreelancerProfileAsync(Guid userId, UpdateFreelancerProfileRequest request)
    {
        var profile = await _db.FreelancerProfiles
            .Include(f => f.User)
            .FirstOrDefaultAsync(f => f.UserId == userId);

        if (profile == null) throw new UnauthorizedAccessException("Profile not found");

        if (request.FullName != null) profile.User.FullName = request.FullName;
        if (request.Tagline != null) profile.Tagline = request.Tagline;
        if (request.Bio != null) profile.Bio = request.Bio;
        if (request.ExperienceLevel != null) profile.ExperienceLevel = request.ExperienceLevel;
        if (request.Availability != null) profile.Availability = request.Availability.Value;

        await _db.SaveChangesAsync();
    }

    public async Task<List<FreelancerListItemResponse>> GetFreelancersListAsync()
    {
        var profiles = await _db.FreelancerProfiles
            .Include(f => f.User)
            .Where(f => f.IsActive && f.Availability)
            .ToListAsync();

        var userIds = profiles.Select(profile => profile.UserId).ToList();
        var skills = await _db.FreelancerSkills
            .Where(skill => userIds.Contains(skill.UserId))
            .Select(skill => new { skill.UserId, skill.SkillId, skill.CustomName })
            .ToListAsync();
        var packagePrices = await _db.FreelancerPackages
            .Where(package => userIds.Contains(package.UserId))
            .GroupBy(package => package.UserId)
            .Select(group => new { UserId = group.Key, StartingPrice = group.Min(package => package.Price) })
            .ToDictionaryAsync(item => item.UserId, item => item.StartingPrice);

        return profiles.Select(p => new FreelancerListItemResponse(
            p.Id, p.User.FullName, p.Tagline, p.Bio, p.CategoryId, p.ExperienceLevel,
            p.Availability, p.AverageRating, p.ReviewCount,
            skills.Where(skill => skill.UserId == p.UserId)
                .Select(skill => new SkillDto { SkillId = skill.SkillId, CustomName = skill.CustomName ?? string.Empty })
                .ToList(),
            packagePrices.TryGetValue(p.UserId, out var startingPrice) ? startingPrice : 0
        )).ToList();
    }

    public async Task<PublicFreelancerProfileResponse> GetPublicFreelancerProfileAsync(Guid id)
    {
        var profile = await _db.FreelancerProfiles
            .Include(f => f.User)
            .FirstOrDefaultAsync(f => f.Id == id);

        if (profile == null) throw new KeyNotFoundException("Profile not found");

        var skills = await _db.FreelancerSkills.Where(s => s.UserId == profile.UserId)
            .Select(s => new ProfileSkillDto(s.Id, s.SkillId, s.CustomName)).ToListAsync();
        var packages = await _db.FreelancerPackages.Where(p => p.UserId == profile.UserId)
            .Select(p => new ProfilePackageDto(p.Id, p.PackageType, p.Price, p.DeliveryDays, p.Description)).ToListAsync();
        var portfolios = await _db.FreelancerPortfolios.Where(p => p.UserId == profile.UserId)
            .Select(p => new PortfolioDto(p.Id, p.Title, p.Description, p.ImageUrl, p.CreatedAt)).ToListAsync();

        return new PublicFreelancerProfileResponse(
            profile.Id, profile.User.FullName, profile.Tagline, profile.Bio,
            profile.IsPhoneVerified, profile.CategoryId, profile.ExperienceLevel,
            profile.Availability, profile.AverageRating, profile.ReviewCount,
            profile.CreatedAt, skills, packages, portfolios);
    }

    public async Task SwitchRoleAsync(Guid userId, string newRole)
    {
        var user = await _db.Users.FindAsync(userId);
        if (user == null) throw new UnauthorizedAccessException("User not found");

        if (newRole == "client") user.Role = Models.Enums.UserRole.Client;
        else if (newRole == "freelancer") user.Role = Models.Enums.UserRole.Freelancer;

        await _db.SaveChangesAsync();
    }
}
