using System;
using System.Threading.Tasks;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebOfWork.Api.DTOs.Projects;
using WebOfWork.Api.Services;

namespace WebOfWork.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProjectsController : ControllerBase
{
    private readonly IProjectService _projectService;
    private readonly IProjectRecommendationService _recommendationService;
    private readonly ICurrentUser _currentUser;

    public ProjectsController(IProjectService projectService, IProjectRecommendationService recommendationService, ICurrentUser currentUser)
    {
        _projectService = projectService;
        _recommendationService = recommendationService;
        _currentUser = currentUser;
    }

    [HttpPost]
    [Authorize(Policy = "RequireClient")]
    public async Task<IActionResult> CreateProject([FromBody] CreateProjectRequest request, [FromServices] IValidator<CreateProjectRequest> validator)
    {
        var validation = await validator.ValidateAsync(request);
        if (!validation.IsValid) return BadRequest(validation.Errors);

        if (_currentUser.Id == null) return Unauthorized();
        var response = await _projectService.CreateProjectAsync(_currentUser.Id.Value, request);
        return Ok(response);
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetProjects([FromQuery] string? search)
    {
        var projects = await _projectService.GetProjectsAsync(search);
        return Ok(projects);
    }

    [HttpGet("{id}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetProjectDetail(int id)
    {
        var project = await _projectService.GetProjectDetailAsync(id);
        if (project == null) return NotFound();
        return Ok(project);
    }

    [HttpPut("{id}")]
    [Authorize(Policy = "RequireClient")]
    public async Task<IActionResult> UpdateProject(int id, [FromBody] UpdateProjectRequest request, [FromServices] IValidator<UpdateProjectRequest> validator)
    {
        var validation = await validator.ValidateAsync(request);
        if (!validation.IsValid) return BadRequest(validation.Errors);

        if (_currentUser.Id == null) return Unauthorized();
        try
        {
            var response = await _projectService.UpdateProjectAsync(_currentUser.Id.Value, id, request);
            return Ok(response);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpDelete("{id}")]
    [Authorize(Policy = "RequireClient")]
    public async Task<IActionResult> DeleteProject(int id)
    {
        if (_currentUser.Id == null) return Unauthorized();
        try
        {
            var deleted = await _projectService.DeleteProjectAsync(_currentUser.Id.Value, id);
            if (!deleted) return NotFound();
            return NoContent();
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    [HttpGet("my")]
    [Authorize(Policy = "RequireClient")]
    public async Task<IActionResult> GetMyProjects()
    {
        if (_currentUser.Id == null) return Unauthorized();
        var projects = await _projectService.GetMyProjectsAsync(_currentUser.Id.Value);
        return Ok(projects);
    }

    [HttpGet("my/{id}")]
    [Authorize(Policy = "RequireClient")]
    public async Task<IActionResult> GetMyProjectDetail(int id)
    {
        if (_currentUser.Id == null) return Unauthorized();
        try
        {
            var project = await _projectService.GetMyProjectDetailAsync(_currentUser.Id.Value, id);
            if (project == null) return NotFound();
            return Ok(project);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    [HttpGet("recommended")]
    [Authorize(Policy = "RequireFreelancer")]
    public async Task<IActionResult> GetRecommendedProjects()
    {
        if (_currentUser.Id == null) return Unauthorized();
        var projects = await _recommendationService.GetRecommendationsAsync(_currentUser.Id.Value);
        return Ok(projects);
    }
}
