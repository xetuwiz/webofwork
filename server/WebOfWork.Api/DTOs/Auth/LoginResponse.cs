using WebOfWork.Api.Models.Enums;
namespace WebOfWork.Api.DTOs.Auth;
public record LoginResponse(string AccessToken, Guid UserId, string Email, string FullName, UserRole Role);
