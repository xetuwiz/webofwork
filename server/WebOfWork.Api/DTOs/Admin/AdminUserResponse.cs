namespace WebOfWork.Api.DTOs.Admin;

public class AdminUserResponse
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public WebOfWork.Api.Models.Enums.UserRole Role { get; set; }
    public WebOfWork.Api.Models.Enums.UserStatus Status { get; set; }
    public bool IsActive { get; set; }
    public bool IsEmailVerified { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // ── Read by userTable.jsx and ViewUserModal.jsx ──
    public string? ProfilePhoto { get; set; }
    /// <summary>Soft-delete flag from ISoftDeletable; drives the "Deleted" chip.</summary>
    public bool IsDeleted { get; set; }
    /// <summary>True for dual client+freelancer accounts ("Both" badge).</summary>
    public bool IsFreelancer { get; set; }
    public bool IsAdmin { get; set; }
    /// <summary>lowercase snake_case, the role label the table falls back to.</summary>
    public string CurrentRole { get; set; } = string.Empty;

    /// <summary>
    /// Not serialized: the raw enum behind <see cref="CurrentRole"/>. EF projects the
    /// enum (it can't call SnakeCase.Of in SQL), then CurrentRole is rendered after.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public WebOfWork.Api.Models.Enums.UserRole CurrentRoleEnum { get; set; }
}
