using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebOfWork.Api.DTOs.Support;
using WebOfWork.Api.Helpers;
using WebOfWork.Api.Models;
using WebOfWork.Api.Models.Enums;
using WebOfWork.Api.Services;

namespace WebOfWork.Api.Controllers;

[ApiController]
[Route("api/common")]
public class CommonController : ControllerBase
{
    private readonly IContentService _content; private readonly ISupportService _support;
    public CommonController(IContentService content,ISupportService support){_content=content;_support=support;}
    [HttpGet("platform-percentage")]
    [AllowAnonymous]
    public async Task<IActionResult> Percentage(CancellationToken ct){var s=await _content.GetGlobalSettingsAsync(ct);return Ok(new{commission_percentage=s.CommissionPercentage.ToString("0.00")});}
    [HttpPost("support/tickets/create")]
    [Authorize]
    public async Task<IActionResult> CreateTicket([FromBody]CreateSupportTicketRequest r,CancellationToken ct)
    {
        var uid=Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);var role=Enum.TryParse<UserRole>(User.FindFirstValue("role")??User.FindFirstValue(ClaimTypes.Role),true,out var parsed)?parsed:UserRole.Client;
        try{return StatusCode(201,await _support.CreateTicketAsync(uid,role,r,ct));}catch(InvalidOperationException){return BadRequest(new{error="You have reached the maximum limit of 5 active support tickets. Please wait for them to be resolved before creating a new one."});}
    }
    [HttpGet("public/terms-and-conditions")]
    [AllowAnonymous]
    public async Task<IActionResult> Terms([FromQuery]int page=1,[FromQuery]int pageSize=10,[FromQuery]int? page_size=null)=>Ok(await Cms(CmsSectionCategory.Terms,page,page_size??pageSize));
    [HttpGet("public/privacy-policy")]
    [AllowAnonymous]
    public async Task<IActionResult> Privacy([FromQuery]int page=1,[FromQuery]int pageSize=10,[FromQuery]int? page_size=null)=>Ok(await Cms(CmsSectionCategory.Privacy,page,page_size??pageSize));
    /// <remarks>
    /// Was hardcoding <c>.Take(10)</c> and returning <c>next = null, previous = null</c>
    /// unconditionally. Every client paginator in the app disables its arrows on a
    /// falsy <c>next</c>/<c>previous</c>, so the public FAQ, Terms and Privacy
    /// pages all had a permanently disabled "Next" -- a visitor could never reach
    /// page 2 no matter how many entries existed.
    /// <para>
    /// The page size is now a parameter (accepting both <c>page_size</c> and
    /// <c>pageSize</c> like the rest of the API) and the links are real.
    /// </para>
    /// </remarks>
    [HttpGet("public/faq")]
    [AllowAnonymous]
    public async Task<IActionResult> Faq([FromQuery] int page = 1, [FromQuery] FaqCategory? category = null, [FromQuery] int pageSize = 10, [FromQuery] int? page_size = null)
    {
        var items = await _content.GetPublicFaqsAsync(category);
        return Ok(Page(items.Select(x => new { id = x.Id, question = x.Question, answer = x.Answer, category = x.Category, created_at = x.CreatedAt }).ToList(), page, items.Count, page_size ?? pageSize));
    }

    private async Task<object> Cms(CmsSectionCategory category, int page, int pageSize)
    {
        var items = await _content.GetPublicCmsByCategoryAsync(category);
        return Page(items.Select(x => new { id = x.Id, category = category == CmsSectionCategory.Terms ? "terms" : "privacy", heading = x.Heading, description = x.Description }).ToList(), page, items.Count, pageSize);
    }

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
