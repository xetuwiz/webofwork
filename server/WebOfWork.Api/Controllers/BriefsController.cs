using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebOfWork.Api.DTOs.Briefs;
using WebOfWork.Api.Services;

namespace WebOfWork.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "RequireClient")]
public class BriefsController : ControllerBase
{
    private readonly IBriefService _briefService;
    private readonly WebOfWork.Api.Services.ICurrentUser _currentUser;

    public BriefsController(IBriefService briefService, WebOfWork.Api.Services.ICurrentUser currentUser)
    {
        _briefService = briefService;
        _currentUser = currentUser;
    }

    [HttpPost]
    public async Task<IActionResult> CreateBrief([FromBody] CreateBriefRequest request)
    {
        if (_currentUser.Id == null) return Unauthorized();

        var response = await _briefService.CreateBriefAsync(_currentUser.Id.Value, request);
        return Ok(response);
    }

    [HttpGet("my")]
    public async Task<IActionResult> GetMyBriefs()
    {
        if (_currentUser.Id == null) return Unauthorized();

        var briefs = await _briefService.GetMyBriefsAsync(_currentUser.Id.Value);
        return Ok(briefs);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetBrief(int id)
    {
        if (_currentUser.Id == null) return Unauthorized();

        try
        {
            var brief = await _briefService.GetBriefDetailAsync(_currentUser.Id.Value, id);
            if (brief == null) return NotFound();
            return Ok(brief);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }
}
