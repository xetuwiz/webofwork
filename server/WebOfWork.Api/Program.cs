using WebOfWork.Api.Integrations.Sms;

namespace WebOfWork.Api;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // ── Controllers ──────────────────────────────────────────────────────────
        builder.Services.AddControllers();

        // ── API Explorer + Swagger ───────────────────────────────────────────────
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();

        // ── Android SMS Gateway — typed HttpClient (Step 0.10) ───────────────────
        // Full credentials wired in Phase 1 via strongly-typed options.
        // Base address set here so the client is resolvable from DI at startup.
        builder.Services.AddHttpClient<AndroidSmsGatewayClient>(client =>
        {
            client.BaseAddress = new Uri(
                builder.Configuration["AndroidSmsGateway:Url"]
                ?? "https://api.sms-gate.app/3rdparty/v1");
        });

        // ────────────────────────────────────────────────────────────────────────
        var app = builder.Build();

        // ── Middleware pipeline ──────────────────────────────────────────────────
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.UseHttpsRedirection();
        app.UseAuthorization();
        app.MapControllers();

        app.Run();
    }
}
