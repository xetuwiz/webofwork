using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebOfWork.Api.Authorization;
using WebOfWork.Api.DTOs.Platform;
using WebOfWork.Api.Services;

namespace WebOfWork.Api.Controllers;

[ApiController]
[Route("api/admin/settings/global")]
[Authorize(Policy = AuthorizationPolicies.RequireAdmin)]
public class PlatformSettingsController : ControllerBase
{
    private readonly IContentService _contentService;
    private readonly IValidator<UpdateGlobalSettingsRequest> _validator;

    public PlatformSettingsController(IContentService contentService, IValidator<UpdateGlobalSettingsRequest> validator)
    {
        _contentService = contentService;
        _validator = validator;
    }

    [HttpGet]
    public async Task<IActionResult> GetSettings(CancellationToken cancellationToken)
    {
        var settings = await _contentService.GetGlobalSettingsAsync(cancellationToken);
        return Ok(settings);
    }

    [HttpPatch]
    public async Task<IActionResult> UpdateSettings([FromBody] UpdateGlobalSettingsRequest request, CancellationToken cancellationToken)
    {
        var validationResult = await _validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            var errors = validationResult.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
                
            return ValidationProblem(new ValidationProblemDetails(errors) { Status = 400 });
        }

        try
        {
            var updated = await _contentService.UpdateGlobalSettingsAsync(request, cancellationToken);
            return Ok(updated);
        }
        catch (ArgumentException ex) when (ex.ParamName == "DefaultCurrency")
        {
            return BadRequest(new ProblemDetails
            {
                Status = 400,
                Title = "Validation Error",
                Detail = ex.Message
            });
        }
    }
}
