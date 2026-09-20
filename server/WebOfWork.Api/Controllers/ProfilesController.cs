using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebOfWork.Api.DTOs.Profiles;
using WebOfWork.Api.Services;

namespace WebOfWork.Api.Controllers;

[ApiController]
[Route("api/profile")]
public class ProfilesController : ControllerBase
{
    private readonly IProfileService _profileService;
    private readonly ICurrentUser _currentUser;

    public ProfilesController(IProfileService profileService, ICurrentUser currentUser)
    {
        _profileService = profileService;
        _currentUser = currentUser;
    }

    [Authorize(Policy = "RequireClient")]
    [HttpGet("/api/profile/me")]
    public async Task<IActionResult> GetClientProfile()
    {
        return Ok(await _profileService.GetClientProfileAsync(_currentUser.Id!.Value));
    }

    [Authorize(Policy = "RequireClient")]
    [HttpPatch("/api/profile/me")]
    public async Task<IActionResult> UpdateClientProfile([FromBody] UpdateClientProfileRequest request)
    {
        await _profileService.UpdateClientProfileAsync(_currentUser.Id!.Value, request);
        return Ok();
    }

    [HttpPost("switch-role")]
    [Authorize]
    public async Task<IActionResult> SwitchRole([FromBody] string newRole)
    {
        await _profileService.SwitchRoleAsync(_currentUser.Id!.Value, newRole);
        return Ok();
    }

    [HttpGet("recommended-freelancers")]
    [Authorize(Policy = "RequireClient")]
    public async Task<IActionResult> GetRecommendedFreelancers()
    {
        var freelancers = await _profileService.GetFreelancersListAsync();
        return Ok(freelancers.Take(5));
    }

}
