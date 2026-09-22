using Microsoft.EntityFrameworkCore;
using WebOfWork.Api.Data;
using WebOfWork.Api.DTOs.Admin;
using WebOfWork.Api.Models;
using WebOfWork.Api.Models.Enums;

namespace WebOfWork.Api.Services;

public interface IAdminUserService
{
    Task<List<AdminUserResponse>> GetUsersAsync(string? search, UserRole? role, UserStatus? status, CancellationToken ct = default);
    Task<AdminUserResponse> ToggleUserActiveAsync(Guid userId, CancellationToken ct = default);
    Task<AdminUserResponse> SoftDeleteUserAsync(Guid userId, CancellationToken ct = default);
}

public class AdminUserService : IAdminUserService
{
    private readonly WebOfWorkDbContext _db;

    public AdminUserService(WebOfWorkDbContext db)
    {
        _db = db;
    }

    public async Task<List<AdminUserResponse>> GetUsersAsync(string? search, UserRole? role, UserStatus? status, CancellationToken ct = default)
    {
        var query = _db.Users.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(u => u.Email.Contains(term) || u.FullName.Contains(term));
        }

        if (role.HasValue)
        {
            query = query.Where(u => u.Role == role.Value);
        }

        if (status.HasValue)
        {
            query = query.Where(u => u.Status == status.Value);
        }

        return await query
            .OrderByDescending(u => u.CreatedAt)
            .Select(u => new AdminUserResponse
            {
                Id = u.Id,
                Email = u.Email,
                FullName = u.FullName,
                Role = u.Role.ToString(),
                Status = u.Status.ToString(),
                IsActive = u.Status == UserStatus.Active,
                IsEmailVerified = u.IsEmailVerified,
                CreatedAt = u.CreatedAt,
                UpdatedAt = u.UpdatedAt
            })
            .ToListAsync(ct);
    }

    public async Task<AdminUserResponse> ToggleUserActiveAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId && !u.IsDeleted, ct)
            ?? throw new KeyNotFoundException($"User {userId} not found.");

        if (user.Role == UserRole.Admin)
        {
            throw new InvalidOperationException("Admin users cannot be blocked or deactivated.");
        }

        user.Status = user.Status == UserStatus.Active ? UserStatus.Blocked : UserStatus.Active;
        await _db.SaveChangesAsync(ct);

        return ToResponse(user);
    }

    public async Task<AdminUserResponse> SoftDeleteUserAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId && !u.IsDeleted, ct)
            ?? throw new KeyNotFoundException($"User {userId} not found.");

        if (user.Role == UserRole.Admin)
        {
            throw new InvalidOperationException("Admin users cannot be soft-deleted.");
        }

        user.IsDeleted = true;
        user.DeletedAt = DateTime.UtcNow;
        user.Status = UserStatus.Deleted;
        await _db.SaveChangesAsync(ct);

        return ToResponse(user);
    }

    private static AdminUserResponse ToResponse(User user) => new()
    {
        Id = user.Id,
        Email = user.Email,
        FullName = user.FullName,
        Role = user.Role.ToString(),
        Status = user.Status.ToString(),
        IsActive = user.Status == UserStatus.Active,
        IsEmailVerified = user.IsEmailVerified,
        CreatedAt = user.CreatedAt,
        UpdatedAt = user.UpdatedAt
    };
}
