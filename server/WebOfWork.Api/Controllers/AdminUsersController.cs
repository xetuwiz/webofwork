using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebOfWork.Api.Helpers;
using WebOfWork.Api.Authorization;
using WebOfWork.Api.Models.Enums;
using WebOfWork.Api.Services;

namespace WebOfWork.Api.Controllers;

[ApiController]
[Route("api/admin/users")]
[Authorize(Policy = AuthorizationPolicies.RequireAdmin)]
public class AdminUsersController : ControllerBase
{
    private readonly IAdminUserService _adminUserService;

    public AdminUsersController(IAdminUserService adminUserService)
    {
        _adminUserService = adminUserService;
    }

    [HttpGet]
    /// <param name="page_size">
    /// Alias for <paramref name="pageSize"/>. This API spells the page size both
    /// ways: CategoriesController and ProjectsController take <c>page_size</c>,
    /// the rest take <c>pageSize</c>. Accepting only one means the other spelling
    /// is silently dropped and the caller gets 20 rows with no indication why.
    /// </param>
    public async Task<IActionResult> ListUsers([FromQuery] string? search, [FromQuery] string? role, [FromQuery] string? status, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] int? page_size = null, CancellationToken ct = default)
    {
        pageSize = Math.Clamp(page_size ?? pageSize, 1, 100);
        UserRole? roleFilter = null;
        if (!string.IsNullOrWhiteSpace(role) && EnumQueryParser.TryParse<UserRole>(role, out var parsedRole))
        {
            roleFilter = parsedRole;
        }

        UserStatus? statusFilter = null;
        if (!string.IsNullOrWhiteSpace(status) && EnumQueryParser.TryParse<UserStatus>(status, out var parsedStatus))
        {
            statusFilter = parsedStatus;
        }

        var users = await _adminUserService.GetPagedUsersAsync(search, roleFilter, statusFilter, page, pageSize, ct);
        PaginationLinks.Apply(Request, users.Page, users.PageSize, users.Count, v => users.Next = v, v => users.Previous = v);
        return Ok(users);
    }

    [HttpPatch("{id:guid}/toggle-active")]
    public async Task<IActionResult> ToggleActive(Guid id, CancellationToken ct)
    {
        try
        {
            var user = await _adminUserService.ToggleUserActiveAsync(id, ct);
            return Ok(user);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpDelete("{id:guid}/soft-delete")]
    public async Task<IActionResult> SoftDelete(Guid id, CancellationToken ct)
    {
        try
        {
            var user = await _adminUserService.SoftDeleteUserAsync(id, ct);
            return Ok(user);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
