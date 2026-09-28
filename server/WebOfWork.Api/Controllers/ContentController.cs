using System.Collections.Generic;
using WebOfWork.Api.Helpers;
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

    [HttpGet("faq")]
    [AllowAnonymous]
    public async Task<IActionResult> GetPublicFaq([FromQuery] string? category, CancellationToken ct)
    {
        FaqCategory? c = null;
        if (!string.IsNullOrEmpty(category) &&
            EnumQueryParser.TryParse<FaqCategory>(category, out var parsed)) c = parsed;
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
        if (EnumQueryParser.TryParse<CmsSectionCategory>(category, out var c))
            return Ok(new { Category = c.ToString(), Items = await _content.GetPublicCmsByCategoryAsync(c, ct) });
        return BadRequest(new { Error = "Invalid CMS category. Use 'terms' or 'privacy'." });
    }

    /// <remarks>
    /// Was returning <c>{ Items, TotalCount }</c> -- PascalCase, and unpaged.
    /// <c>FAQManagement.jsx</c> reads <c>data?.results</c> and <c>data?.count</c>,
    /// which are both undefined on that shape, so the admin FAQ list rendered
    /// <em>nothing at all</em> and the page showed its "no FAQs" empty state to
    /// every admin regardless of how many entries existed. It also ignored
    /// <c>?page=</c>, so its paginator could not have worked either.
    /// <para>
    /// Now returns the same envelope as every other list endpoint, with real
    /// paging and links.
    /// </para>
    /// </remarks>
    [HttpGet("admin/faq")]
    [Authorize(Policy = AuthorizationPolicies.RequireAdmin)]
    public async Task<IActionResult> AdminListFaq([FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] int? page_size = null, CancellationToken ct = default)
    {
        var all = await _content.GetAdminFaqsAsync(ct);
        return Ok(Page(all, page, all.Count, page_size ?? pageSize));
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

    /// <remarks>
    /// Same defect as <c>admin/faq</c>: returned <c>{ Items, TotalCount }</c>
    /// while <c>PrivacyPolicy.jsx</c> and <c>TermAndConditions.jsx</c> read
    /// <c>data?.results</c> / <c>data?.count</c>, so both admin section lists
    /// rendered nothing and displayed their empty state unconditionally.
    /// </remarks>
    [HttpGet("admin/cms")]
    [Authorize(Policy = AuthorizationPolicies.RequireAdmin)]
    public async Task<IActionResult> AdminListCms([FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] int? page_size = null, CancellationToken ct = default)
    {
        var all = await _content.GetAdminCmsSectionsAsync(ct);
        return Ok(Page(all, page, all.Count, page_size ?? pageSize));
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

    /// <summary>
    /// DRF-shaped page over an already-materialised list, with real next/previous
    /// links. <c>CommonController</c> has an equivalent private helper; this one
    /// exists because these two admin actions are in a different controller.
    /// </summary>
    private PagedListResponse<T> Page<T>(IReadOnlyList<T> items, int page, int count, int pageSize)
    {
        var p = Math.Max(page, 1);
        var size = Math.Clamp(pageSize, 1, 100);
        var result = new PagedListResponse<T>
        {
            Count = count,
            Page = p,
            PageSize = size,
            Results = items.Skip((p - 1) * size).Take(size).ToList(),
        };
        PaginationLinks.Apply(Request, result.Page, result.PageSize, result.Count, v => result.Next = v, v => result.Previous = v);
        return result;
    }
}
