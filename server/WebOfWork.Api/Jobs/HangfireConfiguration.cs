using Hangfire;
using Hangfire.PostgreSql;

namespace WebOfWork.Api.Jobs;

/// <summary>
/// Hangfire configuration helper.
/// Uses PostgreSQL storage — same DB as EF Core (no separate broker like Celery/RabbitMQ).
/// Replaces Grolance's Celery + Redis broker pattern.
/// </summary>
public static class HangfireConfiguration
{
    public static IServiceCollection AddHangfireWithPostgres(
        this IServiceCollection services,
        string connectionString)
    {
        services.AddHangfire(config =>
            config
                .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
                .UseSimpleAssemblyNameTypeSerializer()
                .UseRecommendedSerializerSettings()
                .UsePostgreSqlStorage(opts =>
                    opts.UseNpgsqlConnection(connectionString)));

        services.AddHangfireServer(opts =>
        {
            opts.WorkerCount = Environment.ProcessorCount * 2;
            opts.Queues      = ["critical", "default", "low"];
        });

        return services;
    }

    public static IApplicationBuilder UseHangfireDashboardSecured(
        this IApplicationBuilder app,
        string dashboardPath = "/hangfire")
    {
        // Dashboard restricted to Admin role in Phase 13 — for now accessible locally.
        app.UseHangfireDashboard(dashboardPath, new DashboardOptions
        {
            DashboardTitle   = "WebOfWork Jobs",
            // Authorization added in Phase 13 (admin panel).
            Authorization    = []
        });
        return app;
    }
}
