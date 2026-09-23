using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebOfWork.Api.Authorization;
using WebOfWork.Api.DTOs.Content;
using WebOfWork.Api.Models;
using WebOfWork.Api.Services;

namespace WebOfWork.Api.Controllers;

[ApiController]
[Route("api/content")]
public class ContentController : ControllerBase
{
    private readonly IContentService _content;
    private readonly IValidator<UpsertFaqRequest> _faqValidator;
    private readonly IValidator<UpsertCmsSectionRequest> _cmsValidator;

    public ContentController(
        IContentService content,
        IValidator<UpsertFaqRequest> faqValidator,
        IValidator<UpsertCmsSectionRequest> cmsValidator)
    {
        _content = content;
        _faqValidator = faqValidator;
        _cmsValidator = cmsValidator;
    }

    [HttpGet("~/api/common/platform-percentage")]
    [AllowAnonymous]
    public async Task<IActionResult> GetPlatformPercentage(CancellationToken ct)
    {
        var settings = await _content.GetGlobalSettingsAsync(ct);
        return Ok(new { CommissionPercentage = settings.CommissionPercentage, Currency = "USD" });
    }

    [HttpGet("faq")]
    [AllowAnonymous]
    public async Task<IActionResult> GetPublicFaq([FromQuery] string? category, CancellationToken ct)
    {
        FaqCategory? c = null;
        if (!string.IsNullOrEmpty(category) &&
            System.Enum.TryParse<FaqCategory>(category, true, out var parsed)) c = parsed;
        var items = await _content.GetPublicFaqsAsync(c, ct);
        return Ok(new { Items = items, TotalCount = items.Count });
    }

    [HttpGet("terms")]
    [AllowAnonymous]
    public async Task<IActionResult> GetPublicTerms(CancellationToken ct)
    {
        var items = await _content.GetPublicCmsByCategoryAsync(CmsSectionCategory.Terms, ct);
        return Ok(new { Category = "terms", Items = items });
    }

    [HttpGet("privacy")]
    [AllowAnonymous]
    public async Task<IActionResult> GetPublicPrivacy(CancellationToken ct)
    {
        var items = await _content.GetPublicCmsByCategoryAsync(CmsSectionCategory.Privacy, ct);
        return Ok(new { Category = "privacy", Items = items });
    }

    [HttpGet("cms/{category}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetPublicCmsByRoute(string category, CancellationToken ct)
    {
        if (System.Enum.TryParse<CmsSectionCategory>(category, true, out var c))
            return Ok(new { Category = c.ToString(), Items = await _content.GetPublicCmsByCategoryAsync(c, ct) });
        return BadRequest(new { Error = "Invalid CMS category. Use 'terms' or 'privacy'." });
    }

    [HttpGet("admin/faq")]
    [Authorize(Policy = AuthorizationPolicies.RequireAdmin)]
    public async Task<IActionResult> AdminListFaq(CancellationToken ct)
    {
        var items = await _content.GetAdminFaqsAsync(ct);
        return Ok(new { Items = items, TotalCount = items.Count });
    }

    [HttpPost("admin/faq")]
    [Authorize(Policy = AuthorizationPolicies.RequireAdmin)]
    public async Task<IActionResult> AdminCreateFaq([FromBody] UpsertFaqRequest request, CancellationToken ct)
    {
        var v = await _faqValidator.ValidateAsync(request, ct);
        if (!v.IsValid) return ValidationProblem(new ValidationProblemDetails(v.ToDictionary()));
        return Ok(await _content.CreateFaqAsync(request, ct));
    }

    [HttpPut("admin/faq/{id:int}")]
    [Authorize(Policy = AuthorizationPolicies.RequireAdmin)]
    public async Task<IActionResult> AdminUpdateFaq(int id, [FromBody] UpsertFaqRequest request, CancellationToken ct)
    {
        var v = await _faqValidator.ValidateAsync(request, ct);
        if (!v.IsValid) return ValidationProblem(new ValidationProblemDetails(v.ToDictionary()));
        try { return Ok(await _content.UpdateFaqAsync(id, request, ct)); }
        catch (System.Collections.Generic.KeyNotFoundException) { return NotFound($"FAQ #{id} not found."); }
    }

    [HttpDelete("admin/faq/{id:int}")]
    [Authorize(Policy = AuthorizationPolicies.RequireAdmin)]
    public async Task<IActionResult> AdminDeleteFaq(int id, CancellationToken ct)
    {
        var ok = await _content.DeleteFaqAsync(id, ct);
        if (!ok) return NotFound($"FAQ #{id} not found.");
        return Ok(new { Success = true });
    }

    [HttpGet("admin/cms")]
    [Authorize(Policy = AuthorizationPolicies.RequireAdmin)]
    public async Task<IActionResult> AdminListCms(CancellationToken ct)
    {
        var items = await _content.GetAdminCmsSectionsAsync(ct);
        return Ok(new { Items = items, TotalCount = items.Count });
    }

    [HttpPost("admin/cms")]
    [Authorize(Policy = AuthorizationPolicies.RequireAdmin)]
    public async Task<IActionResult> AdminCreateCms([FromBody] UpsertCmsSectionRequest request, CancellationToken ct)
    {
        var v = await _cmsValidator.ValidateAsync(request, ct);
        if (!v.IsValid) return ValidationProblem(new ValidationProblemDetails(v.ToDictionary()));
        return Ok(await _content.CreateCmsSectionAsync(request, ct));
    }

    [HttpPut("admin/cms/{id:int}")]
    [Authorize(Policy = AuthorizationPolicies.RequireAdmin)]
    public async Task<IActionResult> AdminUpdateCms(int id, [FromBody] UpsertCmsSectionRequest request, CancellationToken ct)
    {
        var v = await _cmsValidator.ValidateAsync(request, ct);
        if (!v.IsValid) return ValidationProblem(new ValidationProblemDetails(v.ToDictionary()));
        try { return Ok(await _content.UpdateCmsSectionAsync(id, request, ct)); }
        catch (System.Collections.Generic.KeyNotFoundException) { return NotFound($"CMS Section #{id} not found."); }
    }

    [HttpDelete("admin/cms/{id:int}")]
    [Authorize(Policy = AuthorizationPolicies.RequireAdmin)]
    public async Task<IActionResult> AdminDeleteCms(int id, CancellationToken ct)
    {
        var ok = await _content.DeleteCmsSectionAsync(id, ct);
        if (!ok) return NotFound($"CMS Section #{id} not found.");
        return Ok(new { Success = true });
    }
}
