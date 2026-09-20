using Microsoft.AspNetCore.Authorization;

namespace WebOfWork.Api.Authorization;

/// <summary>
/// Centralized policy definitions.
/// Mirrors Grolance's rest_framework RBAC model (accounts, profiles) ported to ASP.NET Core policies.
/// Role claim names must match what Phase 3 JWT token generation issues.
/// </summary>
public static class AuthorizationPolicies
{
    // ── Policy name constants ─────────────────────────────────────────────────
    public const string RequireClient              = "RequireClient";
    public const string RequireFreelancer          = "RequireFreelancer";
    public const string RequireAdmin               = "RequireAdmin";
    public const string RequireClientOrFreelancer  = "RequireClientOrFreelancer";
    public const string RequireVerified            = "RequireVerified";

    // ── Role claim values (must match Phase 3 token issuance) ────────────────
    public const string RoleClient     = "Client";
    public const string RoleFreelancer = "Freelancer";
    public const string RoleAdmin      = "Admin";
    public const string ClaimRole      = "role";
    public const string ClaimVerified  = "email_verified";

    public static void Configure(AuthorizationOptions options)
    {
        options.AddPolicy(RequireClient, policy =>
            policy.RequireAuthenticatedUser()
                  .RequireRole(RoleClient));

        options.AddPolicy(RequireFreelancer, policy =>
            policy.RequireAuthenticatedUser()
                  .RequireRole(RoleFreelancer));

        options.AddPolicy(RequireAdmin, policy =>
            policy.RequireAuthenticatedUser()
                  .RequireRole(RoleAdmin));

        options.AddPolicy(RequireClientOrFreelancer, policy =>
            policy.RequireAuthenticatedUser()
                  .RequireRole(RoleClient, RoleFreelancer));

        // Phase 3: email verification gate (registration flow)
        options.AddPolicy(RequireVerified, policy =>
            policy.RequireAuthenticatedUser()
                  .RequireClaim(ClaimVerified, "true"));
    }
}
