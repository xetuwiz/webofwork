using System.Security.Claims;

namespace WebOfWork.Api.Services;

public class CurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUser(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    private ClaimsPrincipal? User => _httpContextAccessor.HttpContext?.User;

    public bool IsAuthenticated => User?.Identity?.IsAuthenticated ?? false;

    public Guid? Id 
    {
        get 
        {
            var idClaim = User?.FindFirstValue(ClaimTypes.NameIdentifier);
            if (Guid.TryParse(idClaim, out var id)) return id;
            return null;
        }
    }

    public string? Email => User?.FindFirstValue(ClaimTypes.Email);

    public string? Role => User?.FindFirstValue("role") ?? User?.FindFirstValue(ClaimTypes.Role);

    public bool IsEmailVerified 
    {
        get
        {
            var verifiedClaim = User?.FindFirstValue("email_verified");
            return verifiedClaim != null && bool.TryParse(verifiedClaim, out var isVerified) && isVerified;
        }
    }
}
