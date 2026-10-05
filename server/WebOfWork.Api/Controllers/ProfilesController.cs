using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebOfWork.Api.Data;
using FluentValidation;
using WebOfWork.Api.DTOs.Profiles;
using WebOfWork.Api.Helpers;
using WebOfWork.Api.Integrations.Sms;
using WebOfWork.Api.Validators.Profiles;
using WebOfWork.Api.Models;
using WebOfWork.Api.Models.Enums;
using WebOfWork.Api.Services;

namespace WebOfWork.Api.Controllers;

[ApiController]
[Route("api/profile")]
public class ProfilesController : ControllerBase
{
    private readonly IProfileService _service;
    private readonly IFreelancerOnboardingService _onboarding;
    private readonly IPhoneVerificationService _phone;
    private readonly ICurrentUser _current;
    private readonly WebOfWorkDbContext _db;
    private readonly PhoneOtpValidator _phoneOtpValidator;
    private readonly VerifyPhoneOtpValidator _verifyPhoneOtpValidator;
    private readonly IValidator<FreelancerOnboardingRequest> _onboardingValidator;
    public ProfilesController(IProfileService service, IFreelancerOnboardingService onboarding, IPhoneVerificationService phone, ICurrentUser current, WebOfWorkDbContext db, PhoneOtpValidator phoneOtpValidator, VerifyPhoneOtpValidator verifyPhoneOtpValidator, IValidator<FreelancerOnboardingRequest> onboardingValidator) { _service = service; _onboarding = onboarding; _phone = phone; _current = current; _db = db; _phoneOtpValidator = phoneOtpValidator; _verifyPhoneOtpValidator = verifyPhoneOtpValidator; _onboardingValidator = onboardingValidator; }
    private Guid Id => _current.Id!.Value;

    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> Me() => Ok(await _service.GetClientProfileAsync(Id));
    [HttpPatch("me/update")]
    [Authorize]
    public async Task<IActionResult> Update([FromBody] UpdateClientProfileRequest request) => Ok(await _service.UpdateClientProfileAsync(Id, request));

    [HttpGet("freelancer/me")]
    [Authorize]
    public async Task<IActionResult> SmallFreelancer() => Ok(await Small(Id));
    private async Task<object> Small(Guid userId) { var p = await _db.FreelancerProfiles.AsNoTracking().FirstOrDefaultAsync(x => x.UserId == userId) ?? throw new KeyNotFoundException(); return new { id=p.Id, tagline=p.Tagline, bio=p.Bio, phone=p.Phone, is_phone_verified=p.IsPhoneVerified, category=p.CategoryId, experience_level=p.ExperienceLevel, availability=p.Availability, average_rating=p.AverageRating, review_count=p.ReviewCount }; }

    [HttpGet("freelancer/profile")]
    [Authorize]
    public async Task<IActionResult> BigFreelancer() => Ok(await Big(Id));
    private async Task<object> Big(Guid userId) { var p = await _db.FreelancerProfiles.AsNoTracking().Include(x=>x.User).FirstOrDefaultAsync(x=>x.UserId==userId) ?? throw new KeyNotFoundException(); var skills=await (from fs in _db.FreelancerSkills.Where(x=>x.UserId==userId) join sk in _db.Skills on fs.SkillId equals (int?)sk.Id into sjk from sk in sjk.DefaultIfEmpty() where !string.IsNullOrWhiteSpace(sk==null?null:sk.Name)||!string.IsNullOrWhiteSpace(fs.CustomName) select sk==null||string.IsNullOrWhiteSpace(sk.Name)?(fs.CustomName??"").Trim():sk.Name.Trim()).ToListAsync(); var packages=await _db.FreelancerPackages.Where(x=>x.UserId==userId).ToListAsync(); var ports=await _db.FreelancerPortfolios.Where(x=>x.UserId==userId).Select(x=>new {id=x.Id,title=x.Title,description=x.Description,image_url=x.ImageUrl}).ToListAsync(); var pay=await _db.FreelancerPaymentSettings.AsNoTracking().FirstOrDefaultAsync(x=>x.UserId==userId); // FreelancerProfile.jsx and Profile.jsx render a category label, but the payload only
// carried the raw CategoryId, so the UI printed a bare integer. Projects already expose
// category_name (ProjectsController.cs:116); match that convention instead of renaming the id.
var catName = p.CategoryId == null ? null : await _db.Categories.AsNoTracking().Where(c => c.Id == p.CategoryId).Select(c => c.Name).FirstOrDefaultAsync(); Func<FreelancerPackage,object> tier=x=>new {id=x.Id,package_type=x.PackageType,price=x.Price,delivery_days=x.DeliveryDays,description=(x.Description??"").Split('\n',StringSplitOptions.RemoveEmptyEntries)}; return new { full_name=p.User.FullName, profile_photo=p.User.ProfilePhoto, tagline=p.Tagline,bio=p.Bio,phone=p.Phone,category=p.CategoryId,category_name=catName,experience_level=p.ExperienceLevel,availability=p.Availability,skills=skills,packages=new {starter=packages.FirstOrDefault(x=>x.PackageType=="starter") is { } s ? tier(s):null,pro=packages.FirstOrDefault(x=>x.PackageType=="pro") is { } pr ? tier(pr):null},portfolios=ports,payment_settings=pay==null?null:new {paypal_email=pay.PaypalEmail,is_verified=pay.IsVerified},created_at=p.CreatedAt,completed_projects_count=await _db.Contracts.CountAsync(x=>x.FreelancerId==p.Id&&x.Status==ContractStatus.Completed),review_count=p.ReviewCount,average_rating=p.AverageRating,reviews=await _db.Reviews.Where(r=>r.RevieweeId==userId&&r.Type==ReviewType.Freelancer).OrderByDescending(r=>r.CreatedAt).Select(r=>new {id=r.Id,contract_id=r.ContractId,reviewer_id=r.ReviewerId,reviewee_id=r.RevieweeId,type=r.Type,rating=r.Rating,comment=r.Comment,created_at=r.CreatedAt,reviewer_name=r.Reviewer.FullName,reviewer_photo=r.Reviewer.ProfilePhoto,project_title=r.Contract.Project.Title}).ToListAsync()}; }

    [HttpPost("freelancer/onboarding")]
    [Authorize]
    public async Task<IActionResult> Onboard([FromBody] FreelancerOnboardingRequest request) {
            // FreelancerOnboardingValidator was registered with AddValidatorsFromAssemblyContaining
            // but never invoked, because FluentValidation auto-validation is not configured in
            // Program.cs. So every rule was dead code: a package with delivery_time 0 (or an
            // empty tagline, bad phone) returned 201 and was written straight to the database.
            var validation = await _onboardingValidator.ValidateAsync(request, CancellationToken.None);
            if (!validation.IsValid)
            {
                foreach (var e in validation.Errors)
                    ModelState.AddModelError(e.PropertyName, e.ErrorMessage);
                return ValidationProblem(ModelState);
            }
            await _onboarding.OnboardAsync(Id,request); return StatusCode(201,new {message="Onboarding completed"}); }
    [HttpPost("freelancer/send-phone-otp")]
    [Authorize] public async Task<IActionResult> SendOtp([FromBody] PhoneOtpRequest r)
        {
            var v = await _phoneOtpValidator.ValidateAsync(r);
            if (!v.IsValid) return BadRequest(new { phone = v.Errors.Select(e => e.ErrorMessage).ToArray() });
            await _phone.SendPhoneOtpAsync(Id, r.Phone);
            return Ok(new { phone = SriLankanPhone.Normalize(r.Phone) });
        }
    [HttpPost("freelancer/verify-phone-otp")]
    [Authorize] public async Task<IActionResult> VerifyOtp([FromBody] VerifyPhoneOtpRequest r)
        {
            var v = await _verifyPhoneOtpValidator.ValidateAsync(r);
            if (!v.IsValid) return BadRequest(new { phone = v.Errors.Select(e => e.ErrorMessage).ToArray() });
            await _phone.VerifyPhoneOtpAsync(Id, r.Phone, r.Otp);
            return Ok(new { phone = SriLankanPhone.Normalize(r.Phone) });
        }
    [HttpPost("switch-role")]
    [Authorize] public async Task<IActionResult> Switch([FromBody] SwitchRoleRequest r) { await _service.SwitchRoleAsync(Id,r.Role); return Ok(new {message="Role switched successfully",current_role=r.Role.ToLowerInvariant()}); }
        [HttpGet("freelancers-list")]
        [Authorize]
        /// <param name="search">
        /// Was missing entirely. FindTalent.jsx has always sent <c>?search=</c> and
        /// this action had no parameter to bind it to, so the model binder dropped
        /// it and the search box on /find-talents did nothing. The service takes it
        /// as its first argument now, so every call site must pass it.
        /// </param>
        public async Task<IActionResult> List([FromQuery] string? search, [FromQuery] int? categoryId, [FromQuery] decimal? minPrice, [FromQuery] decimal? maxPrice, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] int? page_size = null)
        {
            var all = await _service.GetFreelancersListAsync(search, categoryId, minPrice, maxPrice);
            // FindTalent.jsx reads data?.results and data?.count, so a bare array renders nothing.
            var p = Math.Max(page, 1);
            // This API spells the page size both ways: CategoriesController and
            // ProjectsController take `page_size`, the rest take `pageSize`.
            // Accepting only one of them means the other spelling is silently
            // dropped and the caller gets 20 rows with no indication why.
            var size = Math.Clamp(page_size ?? pageSize, 1, 100);
            var items = all.Skip((p - 1) * size).Take(size).ToList();

            // next/previous were hardcoded null, and FindTalent.jsx disables its
            // pager on falsy values -- so the page was permanently stuck on page 1
            // no matter how many freelancers existed.
            var result = new PagedListResponse<FreelancerListItemResponse>
            {
                Count = all.Count,
                Page = p,
                PageSize = size,
                Results = items
            };
            PaginationLinks.Apply(Request, p, size, all.Count,
                v => result.Next = v, v => result.Previous = v);
            return Ok(result);
        }
    [HttpGet("freelancer/profile/{id:guid}")]
    [Authorize] public async Task<IActionResult> Public(Guid id) { var p=await _db.FreelancerProfiles.AsNoTracking().Include(x=>x.User).FirstOrDefaultAsync(x=>x.Id==id) ?? throw new KeyNotFoundException(); return Ok(await Big(p.UserId)); }
    [HttpPatch("freelancer/payment-settings")]
    [Authorize] public async Task<IActionResult> Payment([FromBody] PaymentSettingsRequest r) { await _service.UpdatePaymentSettingsAsync(Id,r.PaypalEmail); return Ok(new {paypal_email=r.PaypalEmail,is_verified=true}); }
    [HttpGet("recommended-freelancers")]
    [Authorize] public async Task<IActionResult> Recommended() => Ok((await _service.GetFreelancersListAsync(search: null)).OrderByDescending(x=>x.AverageRating).Take(5));
}

