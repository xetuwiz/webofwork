using System.Security.Claims;
using Hangfire.Dashboard;
using WebOfWork.Api.Authorization;

namespace WebOfWork.Api.Jobs;

/// <summary>
/// Restricts the Hangfire dashboard to administrators.
/// </summary>
/// <remarks>
/// <para>
/// This filter exists because the dashboard was previously registered with
/// <c>Authorization = []</c> — an <em>empty</em> filter list, which means no
/// filters ran, not "no authorization required". The method that mounted it was
/// named <c>UseHangfireDashboardSecured</c> and both of its comments claimed an
/// admin restriction, so the dashboard read as protected in review while being
/// open in production. Job arguments include OTP codes, email addresses and
/// contract ids, so that was an admin surface and a disclosure one.
/// </para>
/// <para>
/// It implements Hangfire's <see cref="IDashboardAuthorizationFilter"/>, whose
/// signature takes a <see cref="DashboardContext"/> rather than an
/// <c>HttpContext</c>. MVC's <c>IAuthorizationFilter</c> looks interchangeable here
/// and is not; <c>DashboardOptions.Authorization</c> is typed to Hangfire's
/// interface, so the wrong one fails to compile rather than silently doing
/// nothing.
/// </para>
/// <para>
/// <b>Why the accessor.</b> <see cref="DashboardContext"/> deliberately exposes no
/// <c>HttpContext</c> and therefore no <c>ClaimsPrincipal</c> — its
/// <c>Request</c> carries only method, path, IPs and query/form values. There is
/// no supported way to reach the authenticated principal through it, so this
/// filter reads <see cref="IHttpContextAccessor.HttpContext"/> instead. That is
/// safe despite the filter being a singleton: the instance is built once at
/// startup, but the context it reads is ambient and per-request. It is the
/// standard workaround for this gap, and it is why the filter must never cache
/// anything from the context in a field.
/// </para>
/// <para>
/// <b>How an admin reaches it.</b> The API is JWT-only — there is no cookie
/// authentication scheme — so a browser opening <c>/hangfire</c> sends no
/// <c>Authorization</c> header and is refused. <c>JwtQueryStringMiddleware</c>
/// promotes <c>?access_token=</c> to a bearer header before authentication runs,
/// the same mechanism the SignalR hubs already rely on, so the URL is
/// <c>/hangfire?access_token=&lt;admin jwt&gt;</c>. Reusing that rather than adding a
/// second mechanism is deliberate; its cost — tokens in query strings reach access
/// logs — is tracked on <c>JwtQueryStringMiddleware</c> and is a wider problem than
/// this filter.
/// </para>
/// </remarks>
public sealed class HangfireAdminAuthorizationFilter : IDashboardAuthorizationFilter
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public HangfireAdminAuthorizationFilter(IHttpContextAccessor httpContextAccessor)
        => _httpContextAccessor = httpContextAccessor;

    public bool Authorize(DashboardContext context)
    {
        var user = _httpContextAccessor.HttpContext?.User;

        if (user is null || user.Identity?.IsAuthenticated != true)
            return false;

        // Both claim spellings are checked because JwtBearerOptions.MapInboundClaims
        // defaults to true and rewrites the JWT's "role" claim to ClaimTypes.Role.
        // That is a default, not a guarantee, and silently depending on it is how
        // an admin panel becomes unreachable with no error anywhere.
        return user.Claims.Any(c =>
            (c.Type == ClaimTypes.Role || c.Type == AuthorizationPolicies.ClaimRole)
            && string.Equals(c.Value, AuthorizationPolicies.RoleAdmin, StringComparison.OrdinalIgnoreCase));
    }
}