using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebOfWork.Api.Services;

namespace WebOfWork.Api.Controllers;

[ApiController]
[Route("api/common")]
[AllowAnonymous]
public class ContentController : ControllerBase
{
    private readonly IContentService _contentService;

    public ContentController(IContentService contentService)
    {
        _contentService = contentService;
    }

    [HttpGet("platform-percentage")]
    public async Task<IActionResult> GetPlatformPercentage(CancellationToken cancellationToken)
    {
        var settings = await _contentService.GetGlobalSettingsAsync(cancellationToken);
        
        return Ok(new
        {
            commissionPercentage = settings.CommissionPercentage,
            currency = settings.DefaultCurrency
        });
    }
}
