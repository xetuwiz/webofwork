using WebOfWork.Api.Common.Interfaces;
using WebOfWork.Api.Models.Enums;

namespace WebOfWork.Api.Models;

public class User : IAuditableEntity, ISoftDeletable
{
    public Guid Id { get; set; }
    
    public string Email { get; set; } = null!;
    public string? PasswordHash { get; set; }
    public string FullName { get; set; } = null!;
    
    public UserRole Role { get; set; }
    public UserStatus Status { get; set; } = UserStatus.Pending;
    
    public bool IsEmailVerified { get; set; } = false;

    public string? Phone { get; set; }
    
    // Refresh Token tracking
    public string? RefreshToken { get; set; }
    public DateTime? RefreshTokenExpiry { get; set; }
    
    // Google Auth integration
    public bool IsGoogleAccount { get; set; } = false;
    public string? GoogleSub { get; set; }

    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
