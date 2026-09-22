using Microsoft.AspNetCore.Authorization;

namespace WebOfWork.Api.Authorization;

public static class AdminAuthorizationPolicy
{
    public const string Name = AuthorizationPolicies.RequireAdmin;

    public static void Configure(AuthorizationOptions options)
    {
        options.AddPolicy(Name, policy =>
            policy.RequireAuthenticatedUser()
                  .RequireRole(AuthorizationPolicies.RoleAdmin));
    }
}
