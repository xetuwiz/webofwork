using Microsoft.AspNetCore.Mvc;
using WebOfWork.Api.Services;

namespace WebOfWork.Api.Controllers;

[ApiController]
[Route("internal/read/platform")]
// Note: Route is protected globally by InternalApiKeyMiddleware in Program.cs. No [Authorize] required.
public class InternalReadController : ControllerBase
{
    private readonly IContentService _contentService;

    public InternalReadController(IContentService contentService)
    {
        _contentService = contentService;
    }

    [HttpGet("agent-thresholds")]
    public async Task<IActionResult> GetAgentThresholds(CancellationToken cancellationToken)
    {
        var settings = await _contentService.GetGlobalSettingsAsync(cancellationToken);
        
        return Ok(new
        {
            minBriefConfidence = settings.MinBriefConfidence,
            proposalScreeningThreshold = settings.ProposalScreeningThreshold,
            matchScoreThreshold = settings.MatchScoreThreshold
        });
    }
}
