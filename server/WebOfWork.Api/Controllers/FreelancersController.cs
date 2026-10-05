using Microsoft.EntityFrameworkCore;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using WebOfWork.Api.DTOs.Profiles;
using WebOfWork.Api.DTOs.Portfolio;
using WebOfWork.Api.Configuration;
using System.Security.Claims;
using WebOfWork.Api.Integrations.Cloudinary;
using WebOfWork.Api.Models.Enums;
using WebOfWork.Api.Services;

namespace WebOfWork.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class FreelancersController : ControllerBase
{
    private readonly IProfileService _profileService;
    private readonly IPhoneVerificationService _phoneService;
    private readonly IFreelancerOnboardingService _onboardingService;
    private readonly IProjectRecommendationService _recommendationService;
    private readonly ICurrentUser _currentUser;

    public FreelancersController(
        IProfileService profileService,
        IPhoneVerificationService phoneService,
        IFreelancerOnboardingService onboardingService,
        IProjectRecommendationService recommendationService,
        ICurrentUser currentUser)
    {
        _profileService = profileService;
        _phoneService = phoneService;
        _onboardingService = onboardingService;
        _recommendationService = recommendationService;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<IActionResult> GetFreelancers([FromQuery] int? categoryId, [FromQuery] decimal? minPrice, [FromQuery] decimal? maxPrice)
    {
        return Ok(await _profileService.GetFreelancersListAsync(null, categoryId, minPrice, maxPrice));
    }

    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetPublicProfile(Guid id)
    {
        return Ok(await _profileService.GetPublicFreelancerProfileAsync(id));
    }

    // NOTE: GET /api/profile/freelancer/me is implemented ONLY in
    // ProfilesController.SmallFreelancer, which returns the reference
    // "small" serializer (id, tagline, bio, phone, is_phone_verified,
    // category, experience_level, availability, average_rating,
    // review_count). It previously also existed here, producing an
    // ambiguous-match 500. The richer management payload remains
    // available at GET /api/profile/freelancer/profile.

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
    [HttpGet("~/api/profile/freelancer/payment-settings")]
    [Authorize(Policy = "RequireFreelancer")]
    public async Task<IActionResult> GetPaymentSettings()
    {
        var db = HttpContext.RequestServices.GetRequiredService<WebOfWork.Api.Data.WebOfWorkDbContext>();
        var settings = await db.Set<WebOfWork.Api.Models.FreelancerPaymentSettings>().FirstOrDefaultAsync(p => p.UserId == _currentUser.Id!.Value);
        return Ok(settings ?? new WebOfWork.Api.Models.FreelancerPaymentSettings());
    }

    [HttpPut("payment-settings")]
    [HttpPut("~/api/profile/freelancer/payment-settings")]
    [Authorize(Policy = "RequireFreelancer")]
    public async Task<IActionResult> UpdatePaymentSettings([FromBody] PaymentSettingsRequest request)
    {
        // Was `[FromBody] string PaypalEmail`: the reference web client PUTs
        // `{"paypal_email": "..."}`, which cannot bind to a bare string, so every
        // save silently stored an empty address. Accept the same object shape
        // the ProfilesController PATCH route does and write through one service.
        await _profileService.UpdatePaymentSettingsAsync(_currentUser.Id!.Value, request.PaypalEmail);
        var db = HttpContext.RequestServices.GetRequiredService<WebOfWork.Api.Data.WebOfWorkDbContext>();
        var settings = await db.Set<WebOfWork.Api.Models.FreelancerPaymentSettings>().FirstOrDefaultAsync(p => p.UserId == _currentUser.Id!.Value);
        return Ok(new { paypal_email = settings?.PaypalEmail ?? request.PaypalEmail, is_verified = settings?.IsVerified ?? true });
    }

    [HttpGet("recommended-projects")]
    [Authorize(Policy = "RequireFreelancer")]
    public async Task<IActionResult> GetRecommendedProjects()
    {
        return Ok(await _recommendationService.GetRecommendationsAsync(_currentUser.Id!.Value));
    }

    public record CreatePortfolioTestDto(string Title, string Description, string ImageUrl);

    [HttpPost("~/api/profile/freelancer/portfolio")]
    [Authorize(Policy = "RequireFreelancer")]
    public async Task<IActionResult> CreatePortfolioItem([FromServices] WebOfWork.Api.Data.WebOfWorkDbContext context, [FromBody] CreatePortfolioTestDto dto)
    {
        var portfolio = new WebOfWork.Api.Models.FreelancerPortfolio
        {
            UserId = Guid.Parse(User.FindFirstValue(System.Security.Claims.ClaimTypes.NameIdentifier)!),
            Title = dto.Title,
            Description = dto.Description,
            ImageUrl = dto.ImageUrl
        };
        context.FreelancerPortfolios.Add(portfolio);
        await context.SaveChangesAsync();
        return Ok(new { id = portfolio.Id });
    }

    /// <summary>Uploads a portfolio image and returns the URL to store in
    /// <c>image_url</c>.</summary>
    /// <remarks>
    /// The web client uploads straight to Cloudinary with an unsigned upload
    /// preset and stores the resulting <c>secure_url</c>. That is a browser
    /// convenience, not a contract: a preset has to be public to be usable from a
    /// page, so shipping one inside a mobile binary turns it into an open upload
    /// bucket for anyone who unzips the APK. This route keeps the credentials
    /// server-side -- it reuses the same <see cref="ICloudinaryStorageService"/>
    /// the deliverable upload already goes through -- and hands the client back
    /// only the URL, which then travels on the ordinary JSON create/PATCH routes.
    /// <para>
    /// Deliberately a *new* route rather than a change to
    /// <c>POST|PATCH .../portfolio</c>: those are <c>[FromBody]</c> JSON, and
    /// switching them to <c>multipart/form-data</c> would 415 the existing web
    /// client.
    /// </para>
    /// </remarks>
    // See SubmitDeliverable for why: Swashbuckle 6.6.2 cannot describe
    // `[FromForm] IFormFile` and fails the entire API description. Hidden from
    // the docs only; routing and the upload are unaffected.
    [ApiExplorerSettings(IgnoreApi = true)]
    [HttpPost("~/api/profile/freelancer/portfolio/upload-image")]
    [Authorize(Policy = "RequireFreelancer")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<IActionResult> UploadPortfolioImage(
            [FromServices] ICloudinaryStorageService cloudinary,
            [FromForm] IFormFile? file)
    {
        if (file is not { Length: > 0 })
            return BadRequest(new { error = "An image file is required." });

        if (file.Length > 10 * 1024 * 1024)
            return BadRequest(new { error = "Image must be 10 MB or smaller." });

        // A multipart POST is attacker-controlled regardless of what the UI
        // offers, so the type is still checked -- but on *either* signal.
        //
        // Content type alone is not enough: Dart's `MultipartFile.fromPath`
        // infers it from the file name, and on Android that inference is
        // unreliable. A real `.jpg` picked from the gallery arrived here as a
        // non-`image/*` content type and was rejected by a content-type-only
        // check, which is the "Only image files are accepted" the mobile user
        // hit on a genuine JPEG. The extension is the other signal the client
        // controls, so accept when either one says image.
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        var looksLikeImage = file.ContentType?.StartsWith("image") == true
            || extension is ".jpg" or ".jpeg" or ".png" or ".webp" or ".gif";

        if (!looksLikeImage)
            return BadRequest(new { error = "Only image files are accepted." });

        try
        {
            using var buffer = new MemoryStream();
            await file.CopyToAsync(buffer);
            var url = await cloudinary.UploadRawAsync(buffer.ToArray(), file.FileName, "portfolios");
            return Ok(new { url });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("~/api/profile/freelancer/portfolio/media")]
    [Authorize(Policy = "RequireFreelancer")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadMedia(
            [FromServices] IPortfolioService portfolioService,
            [FromForm] UploadPortfolioMediaRequest request,
            [FromServices] FluentValidation.IValidator<UploadPortfolioMediaRequest> validator)
    {
        var validationResult = await validator.ValidateAsync(request);
        if (!validationResult.IsValid)
            return BadRequest(validationResult.Errors);

        var freelancerId = Guid.Parse(User.FindFirstValue(System.Security.Claims.ClaimTypes.NameIdentifier)!);
        try
        {
            var result = await portfolioService.UploadMediaAsync(freelancerId, request);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpDelete("~/api/profile/freelancer/portfolio/media/{mediaId}")]
    [Authorize(Policy = "RequireFreelancer")]
    public async Task<IActionResult> DeleteMedia([FromServices] IPortfolioService portfolioService, Guid mediaId)
    {
        var freelancerId = Guid.Parse(User.FindFirstValue(System.Security.Claims.ClaimTypes.NameIdentifier)!);
        try
        {
            await portfolioService.DeleteMediaAsync(freelancerId, mediaId);
            return NoContent();
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>Item-level portfolio update. The reference edit flow only wrote to
    /// React state, so edits were lost on refresh -- there was no PATCH route.</summary>
    [HttpPatch("~/api/profile/freelancer/portfolio/{id:guid}")]
    [Authorize(Policy = "RequireFreelancer")]
    public async Task<IActionResult> UpdatePortfolioItem(Guid id, [FromServices] WebOfWork.Api.Data.WebOfWorkDbContext context, [FromBody] CreatePortfolioTestDto dto)
    {
        var userId = Guid.Parse(User.FindFirstValue(System.Security.Claims.ClaimTypes.NameIdentifier)!);
        var item = await context.FreelancerPortfolios.FirstOrDefaultAsync(p => p.Id == id && p.UserId == userId);
        if (item == null) return NotFound();
        item.Title = dto.Title;
        item.Description = dto.Description;
        item.ImageUrl = dto.ImageUrl;
        await context.SaveChangesAsync();
        return Ok(new { id = item.Id, title = item.Title, description = item.Description, image_url = item.ImageUrl });
    }

    /// <summary>Item-level portfolio delete. Removing the item must also remove its
    /// media rows or the FK constraint trips and the DELETE 500s.</summary>
    [HttpDelete("~/api/profile/freelancer/portfolio/{id:guid}")]
    [Authorize(Policy = "RequireFreelancer")]
    public async Task<IActionResult> DeletePortfolioItem(Guid id, [FromServices] WebOfWork.Api.Data.WebOfWorkDbContext context)
    {
        var userId = Guid.Parse(User.FindFirstValue(System.Security.Claims.ClaimTypes.NameIdentifier)!);
        var item = await context.FreelancerPortfolios.FirstOrDefaultAsync(p => p.Id == id && p.UserId == userId);
        if (item == null) return NotFound();
        var media = await context.PortfolioMedia.Where(m => m.PortfolioItemId == id).ToListAsync();
        context.PortfolioMedia.RemoveRange(media);
        context.FreelancerPortfolios.Remove(item);
        await context.SaveChangesAsync();
        return NoContent();
    }

    [HttpPatch("~/api/profile/freelancer/portfolio/{portfolioItemId}/media/reorder")]
    [Authorize(Policy = "RequireFreelancer")]
    public async Task<IActionResult> ReorderMedia([FromServices] IPortfolioService portfolioService, Guid portfolioItemId, [FromBody] System.Collections.Generic.List<Guid> mediaIds)
    {
        var freelancerId = Guid.Parse(User.FindFirstValue(System.Security.Claims.ClaimTypes.NameIdentifier)!);
        try
        {
            await portfolioService.ReorderMediaAsync(freelancerId, portfolioItemId, mediaIds);
            return NoContent();
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

}
