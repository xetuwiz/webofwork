using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebOfWork.Api.DTOs.Profiles;
using WebOfWork.Api.Services;

namespace WebOfWork.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class FreelancersController : ControllerBase
{
    private readonly IProfileService _profileService;
    private readonly IPhoneVerificationService _phoneService;
    private readonly IFreelancerOnboardingService _onboardingService;
    private readonly ICurrentUser _currentUser;

    public FreelancersController(
        IProfileService profileService,
        IPhoneVerificationService phoneService,
        IFreelancerOnboardingService onboardingService,
        ICurrentUser currentUser)
    {
        _profileService = profileService;
        _phoneService = phoneService;
        _onboardingService = onboardingService;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<IActionResult> GetFreelancers()
    {
        return Ok(await _profileService.GetFreelancersListAsync());
    }

    [Authorize(Policy = "RequireFreelancer")]
    [HttpGet("/api/profile/freelancer/me")]
    public async Task<IActionResult> GetMyProfile()
    {
        return Ok(await _profileService.GetFreelancerProfileAsync(_currentUser.Id!.Value));
    }

    [Authorize(Policy = "RequireFreelancer")]
    [HttpPatch("/api/profile/freelancer/me")]
    public async Task<IActionResult> UpdateMyProfile([FromBody] UpdateFreelancerProfileRequest request)
    {
        await _profileService.UpdateFreelancerProfileAsync(_currentUser.Id!.Value, request);
        return Ok();
    }

    [Authorize(Policy = "RequireFreelancer")]
    [HttpPost("onboard")]
    public async Task<IActionResult> Onboard([FromBody] FreelancerOnboardingRequest request)
    {
        await _onboardingService.OnboardAsync(_currentUser.Id!.Value, request);
        return Ok();
    }

    [Authorize(Policy = "RequireFreelancer")]
    [HttpPost("phone/send-otp")]
    public async Task<IActionResult> SendPhoneOtp([FromBody] PhoneOtpRequest request)
    {
        await _phoneService.SendPhoneOtpAsync(_currentUser.Id!.Value, request.Phone);
        return Ok();
    }

    [Authorize(Policy = "RequireFreelancer")]
    [HttpPost("phone/verify-otp")]
    public async Task<IActionResult> VerifyPhoneOtp([FromBody] VerifyPhoneOtpRequest request)
    {
        await _phoneService.VerifyPhoneOtpAsync(_currentUser.Id!.Value, request.Phone, request.Otp);
        return Ok();
    }


    [HttpPost("skills")]
    [Authorize(Policy = "RequireFreelancer")]
    public async Task<IActionResult> AddSkill([FromBody] WebOfWork.Api.DTOs.Profiles.SkillDto skill)
    {
        var db = HttpContext.RequestServices.GetRequiredService<WebOfWork.Api.Data.WebOfWorkDbContext>();
        var entity = new WebOfWork.Api.Models.FreelancerSkill { UserId = _currentUser.Id!.Value, CustomName = skill.CustomName, SkillId = skill.SkillId };
        db.Set<WebOfWork.Api.Models.FreelancerSkill>().Add(entity);
        await db.SaveChangesAsync();
        return Ok(entity);
    }

    [HttpDelete("skills/{id}")]
    [Authorize(Policy = "RequireFreelancer")]
    public async Task<IActionResult> DeleteSkill(Guid id)
    {
        var db = HttpContext.RequestServices.GetRequiredService<WebOfWork.Api.Data.WebOfWorkDbContext>();
        var skill = await db.Set<WebOfWork.Api.Models.FreelancerSkill>().FirstOrDefaultAsync(s => s.Id == id && s.UserId == _currentUser.Id!.Value);
        if (skill == null) return NotFound();
        db.Set<WebOfWork.Api.Models.FreelancerSkill>().Remove(skill);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("packages")]
    [Authorize(Policy = "RequireFreelancer")]
    public async Task<IActionResult> AddPackage([FromBody] WebOfWork.Api.DTOs.Profiles.PackageDto package)
    {
        var db = HttpContext.RequestServices.GetRequiredService<WebOfWork.Api.Data.WebOfWorkDbContext>();
        var entity = new WebOfWork.Api.Models.FreelancerPackage { UserId = _currentUser.Id!.Value, PackageType = package.PackageType, Price = package.Price, DeliveryDays = package.DeliveryDays, Description = package.Description };
        db.Set<WebOfWork.Api.Models.FreelancerPackage>().Add(entity);
        await db.SaveChangesAsync();
        return Ok(entity);
    }

    [HttpDelete("packages/{id}")]
    [Authorize(Policy = "RequireFreelancer")]
    public async Task<IActionResult> DeletePackage(Guid id)
    {
        var db = HttpContext.RequestServices.GetRequiredService<WebOfWork.Api.Data.WebOfWorkDbContext>();
        var package = await db.Set<WebOfWork.Api.Models.FreelancerPackage>().FirstOrDefaultAsync(p => p.Id == id && p.UserId == _currentUser.Id!.Value);
        if (package == null) return NotFound();
        db.Set<WebOfWork.Api.Models.FreelancerPackage>().Remove(package);
        await db.SaveChangesAsync();
        return NoContent();
    }

    
    [HttpPatch("packages/{id}")]
    [Authorize(Policy = "RequireFreelancer")]
    public async Task<IActionResult> UpdatePackage(Guid id, [FromBody] WebOfWork.Api.DTOs.Profiles.PackageDto package)
    {
        var db = HttpContext.RequestServices.GetRequiredService<WebOfWork.Api.Data.WebOfWorkDbContext>();
        var entity = await db.Set<WebOfWork.Api.Models.FreelancerPackage>().FirstOrDefaultAsync(p => p.Id == id && p.UserId == _currentUser.Id!.Value);
        if (entity == null) return NotFound();
        entity.PackageType = package.PackageType;
        entity.Price = package.Price;
        entity.DeliveryDays = package.DeliveryDays;
        entity.Description = package.Description;
        await db.SaveChangesAsync();
        return Ok(entity);
    }

    [HttpGet("payment-settings")]
    [Authorize(Policy = "RequireFreelancer")]
    public async Task<IActionResult> GetPaymentSettings()
    {
        var db = HttpContext.RequestServices.GetRequiredService<WebOfWork.Api.Data.WebOfWorkDbContext>();
        var settings = await db.Set<WebOfWork.Api.Models.FreelancerPaymentSettings>().FirstOrDefaultAsync(p => p.UserId == _currentUser.Id!.Value);
        return Ok(settings ?? new WebOfWork.Api.Models.FreelancerPaymentSettings());
    }

    [HttpPut("payment-settings")]
    [Authorize(Policy = "RequireFreelancer")]
    public async Task<IActionResult> UpdatePaymentSettings([FromBody] string PaypalEmail)
    {
        var db = HttpContext.RequestServices.GetRequiredService<WebOfWork.Api.Data.WebOfWorkDbContext>();
        var settings = await db.Set<WebOfWork.Api.Models.FreelancerPaymentSettings>().FirstOrDefaultAsync(p => p.UserId == _currentUser.Id!.Value);
        if (settings == null) {
            settings = new WebOfWork.Api.Models.FreelancerPaymentSettings { UserId = _currentUser.Id!.Value, PaypalEmail = PaypalEmail };
            db.Set<WebOfWork.Api.Models.FreelancerPaymentSettings>().Add(settings);
        } else {
            settings.PaypalEmail = PaypalEmail;
        }
        await db.SaveChangesAsync();
        return Ok(settings);
    }

    [HttpGet("recommended-projects")]
    [Authorize(Policy = "RequireFreelancer")]
    public IActionResult GetRecommendedProjects()
    {
        return Ok(new object[] {});
    }

}
