using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
    public async Task<IActionResult> ListUsers([FromQuery] string? search, [FromQuery] string? role, [FromQuery] string? status, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        UserRole? roleFilter = null;
        if (!string.IsNullOrWhiteSpace(role) && Enum.TryParse<UserRole>(role, true, out var parsedRole))
        {
            roleFilter = parsedRole;
        }

        UserStatus? statusFilter = null;
        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<UserStatus>(status, true, out var parsedStatus))
        {
            statusFilter = parsedStatus;
        }

        var users = await _adminUserService.GetPagedUsersAsync(search, roleFilter, statusFilter, page, pageSize, ct);
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
