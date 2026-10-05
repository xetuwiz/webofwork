using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using WebOfWork.Api.DTOs.Internal;
using WebOfWork.Api.Services;

namespace WebOfWork.Api.Controllers;

[ApiController]
[Route("internal/read")]
[EnableRateLimiting("internal-read")]
// Authentication is enforced by InternalApiKeyMiddleware before routing.
public sealed class InternalReadController : ControllerBase
{
    private readonly IContentService _contentService;
    private readonly IInternalReadService _readService;
    private readonly IInternalReadAuditService _audit;

    public InternalReadController(
        IContentService contentService,
        IInternalReadService readService,
        IInternalReadAuditService audit)
    {
        _contentService = contentService;
        _readService = readService;
        _audit = audit;
    }

    [HttpGet("platform/agent-thresholds")]
    public async Task<ActionResult<InternalAgentThresholdsDto>> GetAgentThresholds(CancellationToken ct)
    {
        using var audit = _audit.Begin("platform.agent-thresholds");
        var settings = await _contentService.GetGlobalSettingsAsync(ct);
        return Ok(new InternalAgentThresholdsDto(
            settings.MinBriefConfidence,
            settings.ProposalScreeningThreshold,
            settings.MatchScoreThreshold));
    }

    [HttpGet("platform/commission")]
    public async Task<ActionResult<InternalCommissionDto>> GetCommission(CancellationToken ct)
    {
        using var audit = _audit.Begin("platform.commission");
        return Ok(await _readService.GetCommissionAsync(ct));
    }

    /// <summary>
    /// Reference price bands, for the drafting agent's price suggestion.
    /// </summary>
    /// <remarks>
    /// Read-only, like every route on this controller. The bands are reference
    /// data an admin could edit, not something the agent may change -- an agent
    /// that could write its own price reference would be quoting itself.
    ///
    /// Returns bands, never a single price. The source data is a range and the
    /// whole point of carrying it as a range is that the model has to reason
    /// about where inside it this particular brief sits, which is the judgement
    /// worth having a model do.
    /// </remarks>
    [HttpGet("platform/price-bands")]
    public async Task<ActionResult<IReadOnlyList<InternalPriceBandDto>>> GetPriceBands(
        [FromQuery(Name = "category_id")] int? categoryIdSnake,
        [FromQuery(Name = "categoryId")] int? categoryIdCamel,
        [FromQuery] string? search,
        CancellationToken ct = default)
    {
        // Both spellings, for the same reason `CategoriesController.Skills` does:
        // `SnakeCaseLower` governs JSON only, so a bare `[FromQuery] categoryId`
        // is reachable only as `?categoryId=`. A caller following the project's
        // own wire convention would send `category_id` and silently get
        // everything -- the wrong answer, looking like the right one.
        var categoryId = categoryIdSnake ?? categoryIdCamel;

        using var audit = _audit.Begin("platform.price-bands");
        return Ok(await _readService.GetPriceBandsAsync(categoryId, search, ct));
    }

    /// <summary>
    /// The real skills in one category, for the drafting agent to ground against.
    /// </summary>
    /// <remarks>
    /// Read-only, like every route here. The skill catalogue is admin-managed
    /// reference data; an agent that could edit its own vocabulary would be
    /// choosing the answer from a list it controls.
    ///
    /// Scoped to a single category because the full catalogue is 654 skills and
    /// nesting all of them into the categories payload overflowed the provider's
    /// request limit. One category is a list a model can actually choose from.
    ///
    /// <paramref name="search"/> exists because one category is still sometimes too
    /// much: Development is 199 skills and about 12KB of JSON, which the agent's
    /// tool loop cuts at 4000 characters. Without a way to narrow, the cut falls
    /// mid-list alphabetically and the tail -- JavaScript, React, TypeScript -- is
    /// simply not there. A live draft asked for HTML and JavaScript, could see
    /// neither, and attached Bootstrap instead.
    /// </remarks>
    [HttpGet("categories/{categoryId:int}/skills")]
    public async Task<ActionResult<IReadOnlyList<InternalSkillDto>>> GetCategorySkills(
        int categoryId,
        [FromQuery(Name = "search")] string? search,
        CancellationToken ct = default)
    {
        using var audit = _audit.Begin("category.skills", categoryId.ToString());
        return Ok(await _readService.GetCategorySkillsAsync(categoryId, search, ct));
    }

    [HttpGet("projects/{projectId:int}")]
    public async Task<ActionResult<InternalProjectDto>> GetProject(int projectId, CancellationToken ct)
    {
        using var audit = _audit.Begin("project.read", projectId.ToString());
        var project = await _readService.GetProjectAsync(projectId, ct);
        return project is null ? NotFound() : Ok(project);
    }

    /// <summary>
    /// Recently posted projects in a category, for the planner's comparables check.
    /// </summary>
    /// <remarks>
    /// Distinct from <c>projects/{id}</c>, which is addressed by project. This one
    /// is addressed by category and returns a deliberately narrow projection --
    /// see <see cref="InternalSimilarProjectDto"/> for why it is not the full
    /// project shape.
    ///
    /// The route is declared with a leading slash so it overrides the controller's
    /// <c>api/internal/read</c> prefix; without it the effective path would be
    /// <c>/api/internal/read/projects/by-category/{id}</c> doubled up, which is
    /// the same mistake <c>ProjectsController</c> makes deliberately elsewhere and
    /// documents.
    /// </remarks>
    [HttpGet("/internal/read/projects/by-category/{categoryId:int}")]
    public async Task<ActionResult<IReadOnlyList<InternalSimilarProjectDto>>> SimilarProjects(
        int categoryId,
        [FromQuery] int limit = 10,
        // Snake_case was the only spelling accepted here, so a camelCase caller
        // got every project in the category including the one it was comparing
        // against. Both, as elsewhere on this controller.
        [FromQuery(Name = "exclude_project_id")] int? excludeProjectIdSnake = null,
        [FromQuery(Name = "excludeProjectId")] int? excludeProjectIdCamel = null,
        CancellationToken ct = default)
    {
        var excludeProjectId = excludeProjectIdSnake ?? excludeProjectIdCamel;

        using var audit = _audit.Begin("project.similar", categoryId.ToString());
        var rows = await _readService.GetSimilarProjectsAsync(categoryId, limit, excludeProjectId, ct);
        return Ok(rows);
    }

    [HttpGet("freelancers")]
    public async Task<ActionResult<IReadOnlyList<InternalFreelancerListDto>>> SearchFreelancers(
        [FromQuery] string? query,
        // Both spellings, for the reason `GetPriceBands` gives.
        //
        // The agent client sends `category_id` and `min_rating`, following this
        // project's own wire convention. Query-string binding is case-insensitive
        // but not underscore-insensitive, so those did not bind and both filters
        // were dropped: the matching agent asked for one category above a minimum
        // rating and received every freelancer on the platform, then ranked them.
        // `query`, `available` and `limit` are single words and bound correctly,
        // so the call looked entirely plausible.
        [FromQuery(Name = "category_id")] int? categoryIdSnake,
        [FromQuery(Name = "categoryId")] int? categoryIdCamel,
        [FromQuery(Name = "min_rating")] decimal? minRatingSnake,
        [FromQuery(Name = "minRating")] decimal? minRatingCamel,
        [FromQuery] bool? available,
        [FromQuery] int limit = 50,
        CancellationToken ct = default)
    {
        var categoryId = categoryIdSnake ?? categoryIdCamel;
        var minRating = minRatingSnake ?? minRatingCamel;

        using var audit = _audit.Begin("freelancers.search");
        return Ok(await _readService.SearchFreelancersAsync(query, categoryId, minRating, available, limit, ct));
    }

    [HttpGet("freelancers/{freelancerId:guid}")]
    public async Task<ActionResult<InternalFreelancerProfileDto>> GetFreelancerProfile(Guid freelancerId, CancellationToken ct)
    {
        using var audit = _audit.Begin("freelancer.profile", freelancerId.ToString());
        var profile = await _readService.GetFreelancerProfileAsync(freelancerId, ct);
        return profile is null ? NotFound() : Ok(profile);
    }

    [HttpGet("freelancers/{freelancerId:guid}/reviews")]
    public async Task<ActionResult<IReadOnlyList<InternalReviewDto>>> GetFreelancerReviews(Guid freelancerId, CancellationToken ct)
    {
        using var audit = _audit.Begin("freelancer.reviews", freelancerId.ToString());
        return Ok(await _readService.GetFreelancerReviewsAsync(freelancerId, ct));
    }

    [HttpGet("freelancers/{freelancerId:guid}/packages")]
    public async Task<ActionResult<IReadOnlyList<InternalPackageDto>>> GetFreelancerPackages(Guid freelancerId, CancellationToken ct)
    {
        using var audit = _audit.Begin("freelancer.packages", freelancerId.ToString());
        return Ok(await _readService.GetFreelancerPackagesAsync(freelancerId, ct));
    }

    [HttpGet("contracts/{contractId:int}/disputes")]
    public async Task<ActionResult<IReadOnlyList<InternalDisputeDto>>> GetContractDisputes(int contractId, CancellationToken ct)
    {
        using var audit = _audit.Begin("contract.disputes", contractId.ToString());
        return Ok(await _readService.GetContractDisputesAsync(contractId, ct));
    }

    [HttpGet("briefs/{briefId:int}")]
    public async Task<ActionResult<InternalBriefDto>> GetBrief(int briefId, CancellationToken ct)
    {
        using var audit = _audit.Begin("brief.read", briefId.ToString());
        var brief = await _readService.GetBriefAsync(briefId, ct);
        return brief is null ? NotFound() : Ok(brief);
    }
}
