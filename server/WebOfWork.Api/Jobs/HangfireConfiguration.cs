using Hangfire;
using Hangfire.PostgreSql;

namespace WebOfWork.Api.Jobs;

/// <summary>
/// Hangfire configuration helper.
/// Uses PostgreSQL storage — same DB as EF Core (no separate broker like Celery/RabbitMQ).
/// Replaces the reference Celery + Redis broker pattern.
/// </summary>
public static class HangfireConfiguration
{
    public static IServiceCollection AddHangfireWithPostgres(
        this IServiceCollection services,
        string connectionString)
    {
        services.AddHangfire(config =>
            config.SetDataCompatibilityLevel(CompatibilityLevel.Version_180).UseSimpleAssemblyNameTypeSerializer().UseRecommendedSerializerSettings().UsePostgreSqlStorage(opts =>
                    opts.UseNpgsqlConnection(connectionString)));

        services.AddHangfireServer(opts =>
        {
            opts.WorkerCount = Math.Clamp(Environment.ProcessorCount, 1, 8);
            opts.Queues      = ["critical", "default", "low"];
        });

        // The dashboard's authorization filter needs the ambient HttpContext,
        // because Hangfire's DashboardContext exposes no ClaimsPrincipal.
        // Singleton, like the server above: it holds no per-request state.
        services.AddSingleton<HangfireAdminAuthorizationFilter>();

        return services;
    }

    /// <summary>
    /// Mounts the Hangfire dashboard, restricted to administrators.
    /// </summary>
    /// <param name="app">The application builder.</param>
    /// <param name="dashboardPath">Where to mount it. Defaults to <c>/hangfire</c>.</param>
    /// <remarks>
    /// <b>This method is no longer aspirational.</b> It previously passed
    /// <c>Authorization = []</c> while its comments claimed the dashboard was
    /// restricted to Admin — an empty filter list is <em>no</em> filters, so
    /// <c>/hangfire</c> was readable, triggerable and cancellable by anyone who
    /// could reach the host.
    ///
    /// The filter is resolved from DI rather than constructed inline because it
    /// needs <see cref="IHttpContextAccessor"/>; Hangfire's own
    /// <c>DashboardContext</c> carries no principal. See
    /// <see cref="HangfireAdminAuthorizationFilter"/> for the full reasoning and
    /// for how an admin authenticates to a JWT-only API from a browser.
    /// </remarks>
    public static IApplicationBuilder UseHangfireDashboardSecured(
        this IApplicationBuilder app,
        string dashboardPath = "/hangfire")
    {
        var adminFilter = app.ApplicationServices
            .GetRequiredService<HangfireAdminAuthorizationFilter>();

        app.UseHangfireDashboard(dashboardPath, new DashboardOptions
        {
            DashboardTitle = "WebOfWork Jobs",
            // Not empty any more. An empty array here reads like a decision and
            // behaves like the absence of one, which is how this went unnoticed.
            Authorization = [adminFilter]
        });
        return app;
    }
}
