using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using WebOfWork.Api.Data;
using WebOfWork.Api.DTOs.Profiles;
using WebOfWork.Api.Models;

namespace WebOfWork.Api.Services;

public interface IFreelancerOnboardingService
{
    Task OnboardAsync(Guid userId, FreelancerOnboardingRequest request);
}

public class FreelancerOnboardingService : IFreelancerOnboardingService
{
    private readonly WebOfWorkDbContext _db;

    public FreelancerOnboardingService(WebOfWorkDbContext db)
    {
        _db = db;
    }

    public async Task OnboardAsync(Guid userId, FreelancerOnboardingRequest request)
    {
        var profile = await _db.FreelancerProfiles.FirstOrDefaultAsync(p => p.UserId == userId);
        if (profile == null) throw new UnauthorizedAccessException("Profile not found");

        profile.Tagline = request.Tagline;
        profile.Bio = request.Bio;
        profile.Phone = request.Phone;
        profile.ExperienceLevel = request.ExperienceLevel;
        profile.CategoryId = request.PrimaryCategory;
        profile.IsActive = true;
        
        var existingSkills = _db.FreelancerSkills.Where(s => s.UserId == userId);
        _db.FreelancerSkills.RemoveRange(existingSkills);
        
        foreach (var s in request.Skills)
        {
            _db.FreelancerSkills.Add(new FreelancerSkill { UserId = userId, SkillId = s.SkillId, CustomName = s.CustomName ?? "" });
        }
        
        var existingPackages = _db.FreelancerPackages.Where(p => p.UserId == userId);
        _db.FreelancerPackages.RemoveRange(existingPackages);
        
        foreach (var p in request.Packages)
        {
            _db.FreelancerPackages.Add(new FreelancerPackage { 
                UserId = userId, PackageType = p.PackageType, Price = p.Price, 
                DeliveryDays = p.DeliveryDays, Description = p.Description 
            });
        }
        
        await _db.SaveChangesAsync();
    }
}
