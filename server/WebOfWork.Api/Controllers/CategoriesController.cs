using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebOfWork.Api.DTOs.Categories;
using WebOfWork.Api.Services;

namespace WebOfWork.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CategoriesController : ControllerBase
{
    private readonly ICategoryService _categoryService;

    public CategoriesController(ICategoryService categoryService)
    {
        _categoryService = categoryService;
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetCategories()
    {
        var categories = await _categoryService.GetCategoriesAsync();
        return Ok(categories);
    }

    [HttpPost("create")]
    [Authorize(Policy = "RequireAdmin")]
    public async Task<IActionResult> CreateCategory([FromBody] CreateCategoryRequest request)
    {
        try
        {
            var result = await _categoryService.CreateCategoryAsync(request);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPatch("{id}")]
    [Authorize(Policy = "RequireAdmin")]
    public async Task<IActionResult> UpdateCategory(int id, [FromBody] CreateCategoryRequest request)
    {
        try
        {
            var result = await _categoryService.UpdateCategoryAsync(id, request);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpDelete("{id}")]
    [Authorize(Policy = "RequireAdmin")]
    public async Task<IActionResult> DeleteCategory(int id)
    {
        try
        {
            await _categoryService.DeleteCategoryAsync(id);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("skills")]
    [AllowAnonymous]
    public async Task<IActionResult> GetSkills([FromQuery] int? categoryId)
    {
        var skills = await _categoryService.GetSkillsAsync(categoryId);
        return Ok(skills);
    }

    [HttpPost("skills")]
    [Authorize(Policy = "RequireAdmin")]
    public async Task<IActionResult> CreateSkill([FromBody] CreateSkillRequest request)
    {
        try
        {
            var result = await _categoryService.CreateSkillAsync(request);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPatch("skills/{id}")]
    [Authorize(Policy = "RequireAdmin")]
    public async Task<IActionResult> UpdateSkill(int id, [FromBody] CreateSkillRequest request)
    {
        try
        {
            var result = await _categoryService.UpdateSkillAsync(id, request);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpDelete("skills/{id}")]
    [Authorize(Policy = "RequireAdmin")]
    public async Task<IActionResult> DeleteSkill(int id)
    {
        try
        {
            await _categoryService.DeleteSkillAsync(id);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
