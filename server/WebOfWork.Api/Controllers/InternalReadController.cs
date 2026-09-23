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

    [HttpGet("projects/{projectId:int}")]
    public async Task<ActionResult<InternalProjectDto>> GetProject(int projectId, CancellationToken ct)
    {
        using var audit = _audit.Begin("project.read", projectId.ToString());
        var project = await _readService.GetProjectAsync(projectId, ct);
        return project is null ? NotFound() : Ok(project);
    }

    [HttpGet("freelancers")]
    public async Task<ActionResult<IReadOnlyList<InternalFreelancerListDto>>> SearchFreelancers(
        [FromQuery] string? query,
        [FromQuery] int? categoryId,
        [FromQuery] decimal? minRating,
        [FromQuery] bool? available,
        [FromQuery] int limit = 50,
        CancellationToken ct = default)
    {
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
