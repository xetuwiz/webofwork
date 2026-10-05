using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using WebOfWork.Api.Data;
using WebOfWork.Api.DTOs.Profiles;
using WebOfWork.Api.Models;

namespace WebOfWork.Api.Services;

public interface IFreelancerOnboardingService { Task OnboardAsync(Guid userId, FreelancerOnboardingRequest request); }
public class FreelancerOnboardingService : IFreelancerOnboardingService
{
    private readonly WebOfWorkDbContext _db;
    public FreelancerOnboardingService(WebOfWorkDbContext db) => _db = db;
    public async Task OnboardAsync(Guid userId, FreelancerOnboardingRequest request)
    {
        var p = await _db.FreelancerProfiles.FirstOrDefaultAsync(x=>x.UserId==userId) ?? throw new UnauthorizedAccessException("Phone number not verified");
        if(!p.IsPhoneVerified) throw new UnauthorizedAccessException("Phone number not verified");
        p.Tagline=request.Tagline; p.Bio=request.Bio; p.Phone=request.Phone; p.ExperienceLevel=request.ExperienceLevel; p.CategoryId=request.PrimaryCategory; p.IsActive=true;
        _db.FreelancerSkills.RemoveRange(_db.FreelancerSkills.Where(x=>x.UserId==userId));
        foreach(var s in request.Skills) _db.FreelancerSkills.Add(new FreelancerSkill{UserId=userId,CustomName=s});
        _db.FreelancerPackages.RemoveRange(_db.FreelancerPackages.Where(x=>x.UserId==userId));
        foreach(var x in request.Packages) _db.FreelancerPackages.Add(new FreelancerPackage{UserId=userId,PackageType=x.Key,Price=x.Value.Price,DeliveryDays=x.Value.DeliveryTime,Description=x.Value.Description});
        _db.FreelancerPortfolios.RemoveRange(_db.FreelancerPortfolios.Where(x=>x.UserId==userId));
        foreach(var x in request.Portfolios) _db.FreelancerPortfolios.Add(new FreelancerPortfolio{UserId=userId,Title=x.Title,Description=x.Description,ImageUrl=x.ImageUrl});
        var pay=await _db.FreelancerPaymentSettings.FirstOrDefaultAsync(x=>x.UserId==userId); if(pay==null){pay=new FreelancerPaymentSettings{UserId=userId};_db.FreelancerPaymentSettings.Add(pay);} pay.PaypalEmail=request.PaymentDetails.PaypalEmail;
        var u=await _db.Users.FindAsync(userId); if(u!=null){u.IsFreelancer=true;u.CurrentRole=Models.Enums.UserRole.Freelancer;}
        await _db.SaveChangesAsync();
    }
}
