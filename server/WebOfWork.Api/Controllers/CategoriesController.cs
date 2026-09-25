using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebOfWork.Api.DTOs.Categories;
using WebOfWork.Api.DTOs.Common;
using WebOfWork.Api.Helpers;
using WebOfWork.Api.Services;

namespace WebOfWork.Api.Controllers;

[ApiController]
[Route("api/categories")]
public class CategoriesController : ControllerBase
{
    private readonly ICategoryService _service;
    public CategoriesController(ICategoryService service) => _service = service;

    /// <summary>
    /// <paramref name="no_pagination"/> returns the bare array, which is what the
    /// dropdown consumers want (the web client's <c>categoryApi.js</c> and the mobile
    /// app both ask for it). The paginated branch is the admin table, which sends
    /// <c>page</c> and <c>search</c> -- both of which this used to ignore.
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> List(
        [FromQuery] string? search,
        [FromQuery] bool no_pagination = false,
        [FromQuery] int page = 1,
        [FromQuery] int page_size = 20)
    {
        var all = await _service.GetCategoriesAsync();
        if (!string.IsNullOrWhiteSpace(search))
            all = all.Where(c => c.Name.Contains(search, StringComparison.OrdinalIgnoreCase)).ToList();
        if (no_pagination) return Ok(all);
        return Ok(Page(all, page, page_size));
    }

    [HttpPost("create")]
    [Authorize(Policy = "RequireAdmin")]
    public async Task<IActionResult> Create([FromBody] CreateCategoryRequest request) => Ok(await _service.CreateCategoryAsync(request));

    [HttpGet("{id:int}")]
    [AllowAnonymous]
    public async Task<IActionResult> Get(int id) => Ok(await _service.GetCategoryAsync(id));

    [HttpPatch("{id:int}")]
    [Authorize(Policy = "RequireAdmin")]
    public async Task<IActionResult> Update(int id, [FromBody] CreateCategoryRequest request) => Ok(await _service.UpdateCategoryAsync(id, request));

    [HttpDelete("{id:int}")]
    [Authorize(Policy = "RequireAdmin")]
    public async Task<IActionResult> Delete(int id) { await _service.DeleteCategoryAsync(id); return NoContent(); }

    /// <summary>
    /// The skills catalogue, optionally narrowed to one category or one search term.
    /// </summary>
    /// <remarks>
    /// The category filter is bound from BOTH <c>category_id</c> and
    /// <c>categoryId</c>. The serialiser is configured for snake_case
    /// (<c>PropertyNamingPolicy = SnakeCaseLower</c>), which governs JSON only --
    /// it does not rewrite query-string binding, so <c>[FromQuery] categoryId</c>
    /// is reachable <em>only</em> as <c>?categoryId=</c>. A caller following the
    /// project's own wire convention and sending <c>?category_id=1</c> got no
    /// error and no filter: the full 654-row catalogue instead of the ~90 for that
    /// category. Silent, and wrong in the direction that looks plausible.
    /// Accepting both spellings means neither convention can silently no-op.
    /// </remarks>
    [HttpGet("skills")]
    [AllowAnonymous]
    public async Task<IActionResult> Skills(
        [FromQuery(Name = "category_id")] int? categoryIdSnake,
        [FromQuery(Name = "categoryId")] int? categoryIdCamel,
        [FromQuery] string? search,
        [FromQuery] bool no_pagination = false,
        [FromQuery] int page = 1,
        [FromQuery] int page_size = 20)
    {
        var categoryId = categoryIdSnake ?? categoryIdCamel;
        var all = await _service.GetSkillsAsync(categoryId);
        if (!string.IsNullOrWhiteSpace(search))
            all = all.Where(s => s.Name.Contains(search, StringComparison.OrdinalIgnoreCase)).ToList();
        if (no_pagination) return Ok(all);
        return Ok(Page(all, page, page_size));
    }

    [HttpPost("skills/create")]
    [Authorize(Policy = "RequireAdmin")]
    public async Task<IActionResult> CreateSkill([FromBody] CreateSkillRequest request) => Ok(await _service.CreateSkillAsync(request));

    [HttpGet("skills/{id:int}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetSkill(int id) => Ok(await _service.GetSkillAsync(id));

    [HttpPatch("skills/{id:int}")]
    [Authorize(Policy = "RequireAdmin")]
    public async Task<IActionResult> UpdateSkill(int id, [FromBody] CreateSkillRequest request) => Ok(await _service.UpdateSkillAsync(id, request));

    [HttpDelete("skills/{id:int}")]
    [Authorize(Policy = "RequireAdmin")]
    public async Task<IActionResult> DeleteSkill(int id) { await _service.DeleteSkillAsync(id); return NoContent(); }

    /// <summary>
    /// Was a hand-rolled <c>PageOrList</c> that took <c>.Take(10)</c> with no way to
    /// ask for page 2 and always reported <c>next = null</c>. Two consequences, both
    /// silent: the admin table was stuck on the first 10 rows forever, and the mobile
    /// category dropdown -- which gated its "load more" spinner on <c>next</c> -- could
    /// never page. It also ignored the <c>page</c> and <c>search</c> query parameters
    /// the web client actually sends. Now it builds the shared
    /// <see cref="PagedResponse{T}"/> and fills real next/previous links through
    /// <see cref="PaginationLinks"/>, the same way every other paginated endpoint here
    /// does, so the mobile paginator and the web table both work off one convention.
    /// </summary>
    private PagedResponse<T> Page<T>(IEnumerable<T> values, int page, int pageSize)
    {
        var list = values.ToList();
        var size = Math.Clamp(pageSize, 1, 100);
        var p = Math.Max(page, 1);
        var result = new PagedResponse<T>
        {
            Results = list.Skip((p - 1) * size).Take(size).ToList(),
            Count = list.Count,
            Page = p,
            PageSize = size,
        };
        PaginationLinks.Apply(Request, result.Page, result.PageSize, result.Count, v => result.Next = v, v => result.Previous = v);
        return result;
    }
}
