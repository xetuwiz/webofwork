using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using WebOfWork.Api.Data;
using WebOfWork.Api.DTOs.Profiles;
using WebOfWork.Api.Models;
using WebOfWork.Api.Models.Enums;

namespace WebOfWork.Api.Services;

public interface IProfileService
{
    Task<ClientProfileResponse> GetClientProfileAsync(Guid userId);
    Task<ClientProfileResponse> UpdateClientProfileAsync(Guid userId, UpdateClientProfileRequest request);
    Task<FreelancerProfileResponse> GetFreelancerProfileAsync(Guid userId);
    Task<FreelancerSmallResponse> GetFreelancerSmallAsync(Guid userId);
    Task<FreelancerBigResponse> GetFreelancerBigAsync(Guid userId);
    Task UpdatePaymentSettingsAsync(Guid userId, string paypalEmail);
    Task<ProfilePaymentResponse?> GetPaymentSettingsAsync(Guid userId);
    Task<object> CreateProfileReviewAsync(Guid reviewerId, ReviewCreateRequest request);
    Task<IReadOnlyList<object>> GetProfileReviewsAsync(Guid userId, ReviewType type);
    Task UpdateFreelancerProfileAsync(Guid userId, UpdateFreelancerProfileRequest request);
    /// <param name="search">
    /// Free-text term matched case-insensitively against the freelancer's name,
    /// tagline, bio and skills (both the linked Skill name and the free-text
    /// CustomName). This was missing entirely, so FindTalent.jsx sent
    /// <c>?search=</c> and the API silently ignored it -- the search box on
    /// /find-talents did nothing at all.
    /// </param>
    Task<List<FreelancerListItemResponse>> GetFreelancersListAsync(string? search = null, int? categoryId = null, decimal? minPrice = null, decimal? maxPrice = null);
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
        var profile = await _db.ClientProfiles.Include(c => c.User).Include(c => c.CategoryLinks).ThenInclude(l => l.Category).FirstOrDefaultAsync(c => c.UserId == userId);

        if (profile == null) throw new UnauthorizedAccessException("Profile not found");

        return new ClientProfileResponse(
            profile.Id,
            profile.User.FullName,
            profile.User.Email,
            profile.CompanyName,
            profile.Location,
            profile.CreatedAt,
            profile.User.IsGoogleAccount,
            profile.ReviewCount,
            profile.AverageRating,
            profile.CategoryLinks.Where(l => l.Category != null).Select(l => l.Category.Name).ToList(),
            profile.User.ProfilePhoto
        );
    }

    public async Task<ClientProfileResponse> UpdateClientProfileAsync(Guid userId, UpdateClientProfileRequest request)
    {
        var profile = await _db.ClientProfiles.Include(c => c.User).Include(c => c.CategoryLinks).FirstOrDefaultAsync(c => c.UserId == userId);

        if (profile == null) throw new UnauthorizedAccessException("Profile not found");

        if (request.FullName != null) profile.User.FullName = request.FullName;
        if (request.CompanyName != null) profile.CompanyName = request.CompanyName;
        if (request.Location != null) profile.Location = request.Location;
        if (request.ProfilePhoto != null) profile.User.ProfilePhoto = request.ProfilePhoto;
        // Interests.jsx posts "categories"; the old "category_ids" alias is still honoured.
        var requestedCategoryIds = request.EffectiveCategoryIds;
        if (requestedCategoryIds != null)
        {
            var validIds = await _db.Categories.Where(c => requestedCategoryIds.Contains(c.Id)).Select(c => c.Id).ToListAsync();
            _db.Set<ClientProfileCategory>().RemoveRange(profile.CategoryLinks);
            profile.CategoryLinks.Clear();
            foreach (var categoryId in validIds.Distinct())
                profile.CategoryLinks.Add(new ClientProfileCategory { ClientProfileId = profile.Id, CategoryId = categoryId });
        }

        await _db.SaveChangesAsync();
        return await GetClientProfileAsync(userId);
    }

    public async Task<FreelancerProfileResponse> GetFreelancerProfileAsync(Guid userId)
    {
        var profile = await _db.FreelancerProfiles.Include(f => f.User).FirstOrDefaultAsync(f => f.UserId == userId);

        if (profile == null) throw new UnauthorizedAccessException("Profile not found");

        // Names, not objects (see the note on FreelancerListItemResponse). Joined
        // explicitly because FreelancerSkill has no configured Skill navigation.
        var skills = await (
            from fs in _db.FreelancerSkills.Where(s => s.UserId == userId)
            join sk in _db.Skills on fs.SkillId equals (int?)sk.Id into sjk
            from sk in sjk.DefaultIfEmpty()
            // The reference skips rows with no usable name; the seed data has a few empty ones.
            where !string.IsNullOrWhiteSpace(sk == null ? null : sk.Name) || !string.IsNullOrWhiteSpace(fs.CustomName)
            select sk == null || string.IsNullOrWhiteSpace(sk.Name) ? (fs.CustomName ?? "").Trim() : sk.Name.Trim()
        ).ToListAsync();

        var packages = await _db.FreelancerPackages.Where(p => p.UserId == userId).Select(p => new ProfilePackageDto(p.Id, p.PackageType, p.Price, p.DeliveryDays, p.Description)).ToListAsync();

        var portfolios = await _db.FreelancerPortfolios.Where(p => p.UserId == userId).Select(p => new PortfolioDto(p.Id, p.Title, p.Description, p.ImageUrl, p.CreatedAt)).ToListAsync();

        var completedProjectsCount = await _db.Contracts.CountAsync(c => c.FreelancerId == profile.Id && c.Status == WebOfWork.Api.Models.Enums.ContractStatus.Completed);
        var reviews = await _db.Reviews.AsNoTracking().Where(r => r.RevieweeId == userId).OrderByDescending(r => r.CreatedAt).Take(10).Select(r => new { r.Id, r.ContractId, r.ReviewerId, r.Rating, r.Comment, r.CreatedAt }).ToListAsync();

        return new FreelancerProfileResponse(
            profile.Id, profile.User.FullName, profile.User.Email, profile.Tagline, profile.Bio,
            profile.Phone, profile.IsPhoneVerified, profile.CategoryId, profile.ExperienceLevel,
            profile.Availability, profile.AverageRating, profile.ReviewCount, profile.CreatedAt,
            skills, packages, portfolios, completedProjectsCount, reviews.Cast<object>().ToList()
        );
    }

    public async Task UpdateFreelancerProfileAsync(Guid userId, UpdateFreelancerProfileRequest request)
    {
        var profile = await _db.FreelancerProfiles.Include(f => f.User).FirstOrDefaultAsync(f => f.UserId == userId);

        if (profile == null) throw new UnauthorizedAccessException("Profile not found");

        if (request.FullName != null) profile.User.FullName = request.FullName;
        if (request.Tagline != null) profile.Tagline = request.Tagline;
        if (request.Bio != null) profile.Bio = request.Bio;
        if (request.ExperienceLevel != null) profile.ExperienceLevel = request.ExperienceLevel;
        if (request.Availability != null) profile.Availability = request.Availability.Value;
        if (request.CategoryId.HasValue)
        {
            // Ignore an unknown id rather than throwing on a bad payload.
            if (await _db.Categories.AnyAsync(c => c.Id == request.CategoryId.Value))
                profile.CategoryId = request.CategoryId.Value;
        }

        // Packages and skills were part of the ProfileEdit payload but had no
        // DTO fields, so they were dropped by the binder and the endpoint still
        // returned 200 -- editing price/delivery days appeared to save but never
        // did. Apply them here.
        if (request.Packages is { Count: > 0 })
        {
            foreach (var (tier, incoming) in request.Packages)
            {
                var key = tier?.Trim().ToLowerInvariant();
                if (string.IsNullOrEmpty(key)) continue;
                var pkg = await _db.FreelancerPackages.FirstOrDefaultAsync(p => p.UserId == userId && p.PackageType == key);
                if (pkg == null)
                {
                    pkg = new FreelancerPackage { UserId = userId, PackageType = key };
                    _db.FreelancerPackages.Add(pkg);
                }
                if (incoming.Price > 0m) pkg.Price = incoming.Price;
                if (incoming.DeliveryDays > 0) pkg.DeliveryDays = incoming.DeliveryDays;
                if (incoming.Description != null) pkg.Description = incoming.Description;
            }
        }

        // "is { Count: > 0 }" meant a client sending an empty list -- i.e. the user
        // deleted every skill -- silently kept the old ones, so removing the last
        // skill was impossible from any client. The clients always send the
        // complete list, so a non-null list means "replace with this".
        if (request.Skills is not null)
        {
            var names = request.Skills.Select(s => s?.Trim()).Where(s => !string.IsNullOrEmpty(s)).Select(s => s!).Distinct().ToList();
            if (names.Count > 0)
            {
                _db.FreelancerSkills.RemoveRange(_db.FreelancerSkills.Where(fs => fs.UserId == userId));
                foreach (var name in names)
                {
                    var skill = await _db.Skills.FirstOrDefaultAsync(s => s.Name.ToLower() == name.ToLower());
                    _db.FreelancerSkills.Add(new FreelancerSkill { UserId = userId, SkillId = skill == null ? null : skill.Id, CustomName = skill == null ? name : string.Empty });
                }
            }
        }

        await _db.SaveChangesAsync();
    }

    public async Task<List<FreelancerListItemResponse>> GetFreelancersListAsync(string? search = null, int? categoryId = null, decimal? minPrice = null, decimal? maxPrice = null)
    {
        var profileQuery = _db.FreelancerProfiles.Include(f => f.User).Where(f => f.IsActive && f.Availability).AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            // ILike, not Contains. `Contains` compiles to `LIKE '%term%'`, and
            // Postgres LIKE is case-sensitive under the usual collations, so
            // searching "react" would not match a freelancer tagged "React" or
            // "REACT". The placeholder on the field says "Search skill, name...",
            // which users will type in whatever case they happen to use.
            //
            // Skills are matched as a subquery because Skill has no configured
            // navigation from FreelancerSkills (adding one made EF invent a
            // shadow FK "SkillId1" and fail at runtime), so the join has to be
            // written explicitly -- the same shape used just below.
            var term = search.Trim();
            var pattern = $"%{term}%";
            profileQuery = profileQuery.Where(f =>
                EF.Functions.ILike(f.User.FullName, pattern)
                || EF.Functions.ILike(f.Tagline, pattern)
                || EF.Functions.ILike(f.Bio, pattern)
                || _db.FreelancerSkills.Any(fs => fs.UserId == f.UserId
                    && (EF.Functions.ILike(fs.CustomName, pattern)
                        || (fs.SkillId != null
                            && _db.Skills.Any(s => s.Id == fs.SkillId && EF.Functions.ILike(s.Name, pattern))))));
        }

        if (categoryId.HasValue) profileQuery = profileQuery.Where(f => f.CategoryId == categoryId.Value);
        if (minPrice.HasValue) profileQuery = profileQuery.Where(f => _db.FreelancerPackages.Any(p => p.UserId == f.UserId && p.Price >= minPrice.Value));
        if (maxPrice.HasValue) profileQuery = profileQuery.Where(f => _db.FreelancerPackages.Any(p => p.UserId == f.UserId && p.Price <= maxPrice.Value));
        var profiles = await profileQuery.ToListAsync();

        var userIds = profiles.Select(profile => profile.UserId).ToList();
        // Names, not objects. Resolve via an explicit join on Skill rather than a
        // navigation property: declaring a Skill nav without a configured relationship
        // made EF generate a shadow FK ("SkillId1") and the query failed at runtime.
        var skills = await (
            from fs in _db.FreelancerSkills
            join s in _db.Skills on fs.SkillId equals (int?)s.Id into sj
            from s in sj.DefaultIfEmpty()
            where userIds.Contains(fs.UserId)
            select new { fs.UserId, fs.CustomName, SkillName = s == null ? null : s.Name }
        ).ToListAsync();
        var packagePrices = await _db.FreelancerPackages.Where(package => userIds.Contains(package.UserId)).GroupBy(package => package.UserId).Select(group => new { UserId = group.Key, StartingPrice = group.Min(package => package.Price) }).ToDictionaryAsync(item => item.UserId, item => item.StartingPrice);

        return profiles.OrderByDescending(p => p.AverageRating * 10m + p.TotalEarnings / 100m + p.ReviewCount).Select(p => new FreelancerListItemResponse(
            p.Id, p.User.FullName, p.Tagline, p.Bio, p.CategoryId, p.ExperienceLevel,
            p.Availability, p.AverageRating, p.ReviewCount,
            // The reference returns names, not objects. Prefer the linked Skill's name and
            // fall back to the free-text CustomName when there is no FK.
            skills.Where(skill => skill.UserId == p.UserId).Select(skill => string.IsNullOrWhiteSpace(skill.SkillName) ? skill.CustomName ?? string.Empty : skill.SkillName).Where(name => !string.IsNullOrWhiteSpace(name)).ToList(),
            packagePrices.TryGetValue(p.UserId, out var startingPrice) ? startingPrice : 0,
            // FreelancerCard renders imageUrl, which FindTalent.jsx feeds from
            // profile_photo. Without this every avatar on the page was missing.
            p.User.ProfilePhoto
        )).ToList();
    }

    public async Task<PublicFreelancerProfileResponse> GetPublicFreelancerProfileAsync(Guid id)
    {
        var profile = await _db.FreelancerProfiles.Include(f => f.User).FirstOrDefaultAsync(f => f.Id == id);

        if (profile == null) throw new KeyNotFoundException("Profile not found");

        var skills = await (
            from fs in _db.FreelancerSkills.Where(s => s.UserId == profile.UserId)
            join sk in _db.Skills on fs.SkillId equals (int?)sk.Id into sjk
            from sk in sjk.DefaultIfEmpty()
            // The reference skips rows with no usable name; the seed data has a few empty ones.
            where !string.IsNullOrWhiteSpace(sk == null ? null : sk.Name) || !string.IsNullOrWhiteSpace(fs.CustomName)
            select sk == null || string.IsNullOrWhiteSpace(sk.Name) ? (fs.CustomName ?? "").Trim() : sk.Name.Trim()
        ).ToListAsync();
        var packages = await _db.FreelancerPackages.Where(p => p.UserId == profile.UserId).Select(p => new ProfilePackageDto(p.Id, p.PackageType, p.Price, p.DeliveryDays, p.Description)).ToListAsync();
        var portfolios = await _db.FreelancerPortfolios.Where(p => p.UserId == profile.UserId).Select(p => new PortfolioDto(p.Id, p.Title, p.Description, p.ImageUrl, p.CreatedAt)).ToListAsync();

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

        if (newRole.Equals("client", StringComparison.OrdinalIgnoreCase))
        {
            user.Role = Models.Enums.UserRole.Client;
            user.CurrentRole = Models.Enums.UserRole.Client;
        }
        else if (newRole.Equals("freelancer", StringComparison.OrdinalIgnoreCase))
        {
            user.Role = Models.Enums.UserRole.Freelancer;
            user.CurrentRole = Models.Enums.UserRole.Freelancer;
            user.IsFreelancer = true;
        }
        else
        {
            throw new ArgumentException("Role must be client or freelancer.", nameof(newRole));
        }

        await _db.SaveChangesAsync();
    }


    public async Task<FreelancerSmallResponse> GetFreelancerSmallAsync(Guid userId) { var p=await _db.FreelancerProfiles.AsNoTracking().FirstOrDefaultAsync(x=>x.UserId==userId) ?? throw new KeyNotFoundException(); return new FreelancerSmallResponse(p.Id,p.Tagline,p.Bio,p.Phone,p.IsPhoneVerified,p.CategoryId,p.ExperienceLevel,p.Availability,p.AverageRating,p.ReviewCount); }
    public Task<FreelancerBigResponse> GetFreelancerBigAsync(Guid userId) => throw new NotImplementedException();
    public async Task UpdatePaymentSettingsAsync(Guid userId,string email) { var s=await _db.FreelancerPaymentSettings.FirstOrDefaultAsync(x=>x.UserId==userId); if(s==null){s=new FreelancerPaymentSettings{UserId=userId};_db.FreelancerPaymentSettings.Add(s);} s.PaypalEmail=email; await _db.SaveChangesAsync(); }
    public async Task<ProfilePaymentResponse?> GetPaymentSettingsAsync(Guid userId) { var s=await _db.FreelancerPaymentSettings.AsNoTracking().FirstOrDefaultAsync(x=>x.UserId==userId); return s==null?null:new ProfilePaymentResponse(s.PaypalEmail,s.IsVerified); }
    public async Task<object> CreateProfileReviewAsync(Guid reviewerId, ReviewCreateRequest r) { var type=r.ReviewType.Equals("freelancer",StringComparison.OrdinalIgnoreCase)?ReviewType.Freelancer:ReviewType.Client; var x=new Review{ContractId=r.Contract,ReviewerId=reviewerId,RevieweeId=r.Reviewee,Type=type,Rating=r.Rating,Comment=r.Comment}; _db.Reviews.Add(x); await _db.SaveChangesAsync(); return new {id=x.Id,rating=x.Rating,comment=x.Comment}; }
    public async Task<IReadOnlyList<object>> GetProfileReviewsAsync(Guid userId, ReviewType type) => await _db.Reviews.AsNoTracking().Where(x=>x.RevieweeId==userId&&x.Type==type).OrderByDescending(x=>x.CreatedAt).Select(x=>(object)new {id=x.Id,rating=x.Rating,comment=x.Comment,created_at=x.CreatedAt}).ToListAsync();
    public async Task<List<ParityFreelancerListItemResponse>> GetParityFreelancersListAsync(int? categoryId=null,decimal? minPrice=null,decimal? maxPrice=null) { var ps=_db.FreelancerProfiles.Include(x=>x.User).Where(x=>x.IsActive&&x.Availability); if(categoryId.HasValue)ps=ps.Where(x=>x.CategoryId==categoryId); var a=await ps.ToListAsync(); return a.Select(p=>new ParityFreelancerListItemResponse(p.Id,p.User.FullName,p.User.ProfilePhoto,p.Tagline,p.Bio,null,p.ExperienceLevel,new List<string>(),p.TotalEarnings,p.Availability,p.AverageRating,p.ReviewCount)).ToList(); }
}

