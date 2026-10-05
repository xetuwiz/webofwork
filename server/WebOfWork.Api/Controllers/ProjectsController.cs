using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebOfWork.Api.Data;
using WebOfWork.Api.DTOs.Projects;
using WebOfWork.Api.Helpers;
using WebOfWork.Api.Models.Enums;
using WebOfWork.Api.Services;

namespace WebOfWork.Api.Controllers;

[ApiController]
[Route("api/projects")]
public class ProjectsController : ControllerBase
{
    private readonly IProjectService _service; private readonly IProjectRecommendationService _recommendations; private readonly ICurrentUser _user; private readonly WebOfWorkDbContext _db;
    public ProjectsController(IProjectService service, IProjectRecommendationService recommendations, ICurrentUser user, WebOfWorkDbContext db) { _service=service; _recommendations=recommendations; _user=user; _db=db; }

    [HttpPost("create")]
    [Authorize(Policy="RequireClient")]
    public async Task<IActionResult> Create([FromBody] CreateProjectRequest request) { try { return Ok(await _service.CreateProjectAsync(_user.Id!.Value,request)); } catch(Exception e) { return ToErrorResult(e); } }

    /// <summary>
    /// this route backs "My Posts", so it is scoped to the caller's own projects.
    /// The public open-project browse feed that used to live here is preserved on
    /// <see cref="Browse"/> for any consumer that needs it.
    /// </summary>
    [HttpGet]
    [Authorize]
    public async Task<IActionResult> List([FromQuery] string? search, [FromQuery] string? status, [FromQuery] int page=1, [FromQuery] int page_size=20)
    {
        ProjectStatus? statusFilter = null;
        if (!string.IsNullOrWhiteSpace(status) && EnumQueryParser.TryParse<ProjectStatus>(status, out var parsed)) statusFilter = parsed;
        var result = await _service.GetPagedMyProjectsAsync(_user.Id!.Value, search, statusFilter, page, page_size);
        PaginationLinks.Apply(Request, result.Page, result.PageSize, result.Count, v => result.Next = v, v => result.Previous = v);
        return Ok(result);
    }

    [HttpGet("browse")]
    [Authorize]
    public async Task<IActionResult> Browse([FromQuery] string? search, [FromQuery] int page=1, [FromQuery] int page_size=20)
    {
        var result = await _service.GetPagedProjectsAsync(search, null, null, null, null, null, page, page_size);
        PaginationLinks.Apply(Request, result.Page, result.PageSize, result.Count, v => result.Next = v, v => result.Previous = v);
        return Ok(result);
    }

    [HttpGet("{id:int}")]
    [AllowAnonymous]
    public async Task<IActionResult> Get(int id) { var p=await _service.GetProjectDetailAsync(id); return p==null?NotFound():Ok(p); }

    /// <summary>
    /// Maps a service exception to a status code.
    /// </summary>
    /// <remarks>
    /// The catch-all `catch (Exception) =&gt; BadRequest` this replaces turned every
    /// failure into 400, including "you do not own this project". That is the wrong
    /// signal: 400 tells a client to fix its input, and retrying a corrected payload
    /// will fail identically forever. 403 is what lets the UI distinguish "your
    /// request is malformed" from "this isn't yours", which is the only reason to
    /// branch on the code at all.
    ///
    /// The same mapping the global <c>ExceptionHandlingMiddleware</c> applies, kept
    /// here because these actions return a JSON <c>{ error }</c> body rather than
    /// ProblemDetails. The one deliberate difference: unexpected exceptions stay
    /// 400 rather than becoming a 500, because the service layer throws bare
    /// <c>InvalidOperationException</c> for business-rule violations and there is
    /// currently no way to tell those apart from a genuine fault.
    /// </remarks>
    private IActionResult ToErrorResult(Exception e) => e switch
    {
        UnauthorizedAccessException => StatusCode(StatusCodes.Status403Forbidden, new { error = e.Message }),
        KeyNotFoundException        => NotFound(new { error = e.Message }),
        ArgumentException           => BadRequest(new { error = e.Message }),
        _                           => BadRequest(new { error = e.Message }),
    };

    [HttpPatch("{id:int}")]
    [Authorize(Policy="RequireClient")]
    public async Task<IActionResult> Update(int id,[FromBody] UpdateProjectRequest r) { try{return Ok(await _service.UpdateProjectAsync(_user.Id!.Value,id,r));}catch(Exception e){return ToErrorResult(e);} }
    [HttpDelete("{id:int}")]
    [Authorize(Policy="RequireClient")]
    public async Task<IActionResult> Delete(int id) { return await _service.DeleteProjectAsync(_user.Id!.Value,id)?NoContent():NotFound(); }

    // This used to return a bare array while every other list endpoint (contracts,
    // categories, admin users) and the frontend expect the paged envelope, so the
    // UI read data.results/data.count as undefined and rendered "0 jobs found".
    //
    // It also omitted `next`/`previous` entirely. FindJobs.jsx gates its paginator
    // on `disabled={!data?.next}`, so an absent key is a permanently disabled Next
    // and the user could never leave page 1.
    [HttpGet("freelancer/project-list")]
    [Authorize(Policy="RequireFreelancer")]
    public async Task<IActionResult> FreelancerList([FromQuery] string? search,[FromQuery] int? category,[FromQuery] string? skill,[FromQuery] decimal? min_price,[FromQuery] decimal? max_price,[FromQuery] int? delivery_days,[FromQuery] int page=1,[FromQuery] int page_size=20) { if (_user.Id is not Guid userId) return Unauthorized(); var q=_db.Projects.Include(p=>p.Category).Include(p=>p.Skills).ThenInclude(x=>x.Skill).Where(p=>p.Status==WebOfWork.Api.Models.Enums.ProjectStatus.Open&&p.IsActive&&p.ClientId!=userId); if(!string.IsNullOrWhiteSpace(search)){var pat=$"%{search.Trim()}%"; q=q.Where(p=>EF.Functions.ILike(p.Title,pat)||EF.Functions.ILike(p.Description,pat));} if(category.HasValue)q=q.Where(p=>p.CategoryId==category); if(!string.IsNullOrWhiteSpace(skill))q=q.Where(p=>p.Skills.Any(x=>(x.Skill!=null&&x.Skill!.Name.ToLower()==skill.ToLower())||(x.CustomName!=null&&x.CustomName.ToLower()==skill.ToLower()))); if(min_price.HasValue)q=q.Where(p=>(p.FixedPrice??p.MinPrice??0)>=min_price); if(max_price.HasValue)q=q.Where(p=>(p.FixedPrice??p.MaxPrice??0)<=max_price); if(delivery_days.HasValue)q=q.Where(p=>p.ExpectedDays<=delivery_days); var total=await q.CountAsync(); var size=Math.Clamp(page_size,1,100);var pnum=Math.Max(page,1); var items=await q.OrderByDescending(p=>p.CreatedAt).Skip((pnum-1)*size).Take(size).Select(p=>new {id=p.Id,title=p.Title,description=p.Description,category=p.CategoryId,category_name=p.Category==null?null:p.Category.Name,pricing_type=WebOfWork.Api.Helpers.SnakeCase.Of(p.PricingType),fixed_price=p.FixedPrice,min_budget=p.MinPrice,max_budget=p.MaxPrice,delivery_days=p.ExpectedDays,status=WebOfWork.Api.Helpers.SnakeCase.Of(p.Status),created_at=p.CreatedAt,skills=p.Skills.Select(s=>s.Skill==null?s.CustomName:s.Skill.Name).ToList()}).ToListAsync(); var result = new WebOfWork.Api.Helpers.PagedListResponse<object> { Count = total, Page = pnum, PageSize = size, Results = items.Cast<object>().ToList() }; WebOfWork.Api.Helpers.PaginationLinks.Apply(Request, pnum, size, total, v => result.Next = v, v => result.Previous = v); return Ok(result); }

    /// The freelancer-shaped project detail.
    ///
    /// Two fields were added here rather than invented client-side:
    ///  * <c>is_applied</c> -- the job-details screen has to know whether to
    ///    offer "Apply" or "Already applied". It previously derived that from
    ///    <c>check_proposal_send_or_not</c>, a template column this API does not
    ///    have, so the lookup was always null and the comparison against "0"
    ///    always succeeded: the button rendered permanently disabled.
    ///  * <c>proposals_count</c> -- the same reason; the count used to come from
    ///    a nested <c>job_proposals</c> array that does not exist, so it always
    ///    rendered as zero.
    /// Both are scoped to the caller, so a client opening their own post still
    /// sees a truthful "not applied" rather than an error.
    /// </summary>
    [HttpGet("{id:int}/details")]
    [Authorize(Policy="RequireFreelancer")]
    public async Task<IActionResult> Details(int id)
    {
        var p = await _db.Projects
            .Include(p => p.Category)
            .Include(p => p.Client).ThenInclude(c => c.User)
            .Include(p => p.Skills).ThenInclude(s => s.Skill)
            .FirstOrDefaultAsync(x => x.Id == id && x.IsActive);
        if (p == null) return NotFound();

        // A freelancer with no profile row yet simply has not applied; that is
        // not an error, so a missing profile is treated as "not applied" rather
        // than thrown, unlike ProposalService.GetFreelancerIdAsync.
        var freelancerId = _user.Id is Guid uid
            ? await _db.FreelancerProfiles
                .Where(f => f.UserId == uid)
                .Select(f => (Guid?)f.Id)
                .FirstOrDefaultAsync()
            : null;

        var proposalsCount = await _db.Proposals.CountAsync(x => x.ProjectId == p.Id);
        var isApplied = freelancerId.HasValue &&
            await _db.Proposals.AnyAsync(x => x.ProjectId == p.Id && x.FreelancerId == freelancerId.Value);

        return Ok(new
        {
            id = p.Id,
            title = p.Title,
            description = p.Description,
            category = p.CategoryId,
            category_name = p.Category == null ? null : p.Category.Name,
            pricing_type = WebOfWork.Api.Helpers.SnakeCase.Of(p.PricingType),
            fixed_price = p.FixedPrice,
            min_budget = p.MinPrice,
            max_budget = p.MaxPrice,
            delivery_days = p.ExpectedDays,
            expected_deliverables = p.ExpectedDeliverables,
            requirements = p.Requirements,
            status = WebOfWork.Api.Helpers.SnakeCase.Of(p.Status),
            created_at = p.CreatedAt,
            is_active = p.IsActive,
            proposals_count = proposalsCount,
            is_applied = isApplied,
            skills = p.Skills.Select(s => s.Skill == null ? s.CustomName : s.Skill.Name).ToList(),
            client_info = new
            {
                id = p.ClientId,
                full_name = p.Client?.User?.FullName,
                profile_photo = p.Client?.User?.ProfilePhoto,
                company_name = p.Client?.CompanyName,
                average_rating = p.Client?.AverageRating,
                review_count = p.Client?.ReviewCount,
                total_jobs_posted = await _db.Projects.CountAsync(x => x.ClientId == p.ClientId)
            }
        });
    }

    [HttpGet("/api/profile/freelancer/recommended-projects")]
    [Authorize(Policy="RequireFreelancer")]
    public async Task<IActionResult> Recommended() => Ok(await _recommendations.GetRecommendationsAsync(_user.Id!.Value));
}
