using System.Text;
using Hangfire;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;
using StackExchange.Redis;
using WebOfWork.Api.Authorization;
using WebOfWork.Api.Configuration;
using WebOfWork.Api.Hubs;
using WebOfWork.Api.Integrations.Email;
using WebOfWork.Api.Integrations.Sms;
using WebOfWork.Api.Integrations.Push;
using WebOfWork.Api.Jobs;
using WebOfWork.Api.Middleware;
using Microsoft.EntityFrameworkCore;
using FluentValidation;

namespace WebOfWork.Api;

public class Program
{
    public static void Main(string[] args)
    {
        // ── Serilog Bootstrap (Step 1.8) ─────────────────────────────────────────
        Log.Logger = new LoggerConfiguration()
            .WriteTo.Console()
            .CreateBootstrapLogger();

        try
        {
            var appEnv = Environment.GetEnvironmentVariable("APP_ENV");
            var checkpointBackend = Environment.GetEnvironmentVariable("CHECKPOINT_BACKEND");

            // `ASPNETCORE_ENVIRONMENT` counts as well as `APP_ENV`.
            //
            // These are different variables and it is easy to assume they are the
            // same: `ASPNETCORE_ENVIRONMENT` is what selects Development vs
            // Production configuration and what `IHostEnvironment` reads, while
            // `APP_ENV` is this project's own switch. Deploying with only the
            // former -- which is the conventional way, and what a platform
            // default would do -- left this guard not firing at all, so the one
            // check that is supposed to fail closed in production was the one
            // check that did not run.
            var isProduction =
                string.Equals(appEnv, "production", StringComparison.OrdinalIgnoreCase)
                || string.Equals(
                    Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"),
                    "production",
                    StringComparison.OrdinalIgnoreCase);

            if (isProduction &&
                !string.Equals(checkpointBackend, "postgres", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Production requires CHECKPOINT_BACKEND=postgres to fail closed.");
            }

            var builder = WebApplication.CreateBuilder(args);

            // ── Host Configuration ───────────────────────────────────────────────
            builder.Host.UseSerilog((context, services, configuration) => configuration
                .ReadFrom.Configuration(context.Configuration)
                .ReadFrom.Services(services)
                .Enrich.FromLogContext()
                .WriteTo.Console()
                .WriteTo.File("logs/project_activity.log", rollingInterval: RollingInterval.Day));

            // ── Options Binding (Step 1.2) ───────────────────────────────────────
            builder.Services.Configure<DatabaseOptions>(builder.Configuration.GetSection(DatabaseOptions.Section));
            builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.Section));
            builder.Services.Configure<RedisOptions>(builder.Configuration.GetSection(RedisOptions.Section));
            builder.Services.Configure<MailOptions>(builder.Configuration.GetSection(MailOptions.Section));
            builder.Services.Configure<AndroidSmsGatewayOptions>(builder.Configuration.GetSection(AndroidSmsGatewayOptions.Section));
            builder.Services.Configure<PayPalOptions>(builder.Configuration.GetSection(PayPalOptions.Section));
            builder.Services.Configure<CloudinaryOptions>(builder.Configuration.GetSection(CloudinaryOptions.SectionName));
            builder.Services.Configure<GoogleOptions>(builder.Configuration.GetSection(GoogleOptions.Section));
            builder.Services.Configure<AgenticAiOptions>(builder.Configuration.GetSection(AgenticAiOptions.Section));
            builder.Services.Configure<InternalApiOptions>(builder.Configuration.GetSection(InternalApiOptions.Section));
            builder.Services.Configure<LlmOptions>(builder.Configuration.GetSection(LlmOptions.Section));
            builder.Services.Configure<PlatformOptions>(builder.Configuration.GetSection(PlatformOptions.Section));

            var dbOptions = builder.Configuration.GetSection(DatabaseOptions.Section).Get<DatabaseOptions>() ?? new DatabaseOptions();
            var jwtOptions = builder.Configuration.GetSection(JwtOptions.Section).Get<JwtOptions>() ?? new JwtOptions();

            // ── Redis Multiplexer (Step 1.11) ────────────────────────────────────
            if (!string.IsNullOrEmpty(dbOptions.Redis))
            {
                builder.Services.AddSingleton<IConnectionMultiplexer>(ConnectionMultiplexer.Connect(dbOptions.Redis));
            }

            // ── Controllers & SignalR (Step 1.13) ────────────────────────────────
            builder.Services.AddControllers().AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.SnakeCaseLower;
                options.JsonSerializerOptions.DictionaryKeyPolicy = System.Text.Json.JsonNamingPolicy.SnakeCaseLower;
                options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter(System.Text.Json.JsonNamingPolicy.SnakeCaseLower));
                options.JsonSerializerOptions.Converters.Add(new DecimalAsStringJsonConverter());
                options.JsonSerializerOptions.Converters.Add(new IsoDateTimeJsonConverter());
            });
            builder.Services.AddSignalR();
            builder.Services.AddRateLimiter(options =>
            {
                options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
                options.AddPolicy(
                    InternalReadRateLimitPolicy.PolicyName,
                    InternalReadRateLimitPolicy.Create);
            });

            // ── Database Context (Phase 2) ───────────────────────────────────────
            if (!string.IsNullOrEmpty(dbOptions.Postgres))
            {
                builder.Services.AddDbContext<WebOfWork.Api.Data.WebOfWorkDbContext>(opts =>
                    opts.UseNpgsql(dbOptions.Postgres)
                        .UseSnakeCaseNamingConvention());
            }

            // ── CORS (Step 1.9) ──────────────────────────────────────────────────
            builder.Services.AddCors(options =>
            {
                var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
                options.AddDefaultPolicy(policy =>
                    policy.WithOrigins(allowedOrigins)
                          .AllowAnyHeader()
                          .AllowAnyMethod()
                          .AllowCredentials());

                // `AllowCredentials` plus an empty origin list is the worst of both
                // worlds: every cross-origin request is rejected, and the browser
                // reports it as a CORS failure rather than a configuration one.
                //
                // This used to be silently wrong in Production. The base
                // appsettings.json allows only `http://localhost:5173` for local
                // dev, and `appsettings.Production.json` had no `Cors` section at
                // all -- and configuration merges, so Production inherited the
                // localhost value. A Vercel-hosted frontend therefore got 403 from
                // the API on every call, with nothing in the logs to say why.
                //
                // Not thrown: this is a functional misconfiguration rather than an
                // unsafe one, and refusing to start would make it harder to reach
                // Swagger to diagnose. An explicit error at startup is the right
                // severity -- loud, actionable, and non-blocking.
                if (builder.Environment.IsProduction()
                    && (allowedOrigins.Length == 0
                        || allowedOrigins.All(o => o.Contains("localhost", StringComparison.OrdinalIgnoreCase)
                                              || o.Contains("127.0.0.1", StringComparison.OrdinalIgnoreCase))))
                {
                    Log.Error(
                        "CORS is configured for local development only ({Origins}) while running in Production. " +
                        "Every browser request from the deployed frontend will be rejected. Set the real origin " +
                        "via the environment variable Cors__AllowedOrigins__0=https://your-frontend-host, or add a " +
                        "Cors section to appsettings.Production.json.",
                        allowedOrigins.Length == 0 ? "<none>" : string.Join(", ", allowedOrigins));
                }
            });

            // ── Forwarded Headers (ngrok / Railway reverse proxy) ────────────────
            builder.Services.Configure<ForwardedHeadersOptions>(options =>
            {
                options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;
                options.RequireHeaderSymmetry = false;
                options.ForwardLimit = null;
                options.KnownProxies.Clear();
                options.KnownNetworks.Clear();
            });

            // ── Health Checks (Step 1.7) ─────────────────────────────────────────
            var health = builder.Services.AddHealthChecks();
            if (!string.IsNullOrEmpty(dbOptions.Postgres))
                health.AddNpgSql(dbOptions.Postgres, name: "postgres");
            if (!string.IsNullOrEmpty(dbOptions.Redis))
                health.AddRedis(dbOptions.Redis, name: "redis");

            // ── Hangfire (Step 1.12) ─────────────────────────────────────────────
            if (!string.IsNullOrEmpty(dbOptions.Postgres))
            {
                builder.Services.AddHangfireWithPostgres(dbOptions.Postgres);
            }

            // ── API Explorer + Swagger with JWT (Step 1.6) ───────────────────────
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo { Title = "WebOfWork API", Version = "v1" });
                c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
                {
                    Description = "JWT Authorization header using the Bearer scheme. Example: \"Bearer {token}\"",
                    Name = "Authorization",
                    In = ParameterLocation.Header,
                    Type = SecuritySchemeType.ApiKey,
                    Scheme = "Bearer"
                });
                c.AddSecurityRequirement(new OpenApiSecurityRequirement
                {
                    {
                        new OpenApiSecurityScheme
                        {
                            Reference = new OpenApiReference
                            {
                                Type = ReferenceType.SecurityScheme,
                                Id = "Bearer"
                            }
                        },
                        Array.Empty<string>()
                    }
                });
            });

            // ── Authentication & Authorization (Step 1.10) ───────────────────────

            // Prevent default renaming of claims (e.g. "role" to "http://schemas.microsoft.com/ws/...")
            System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();

            if (!string.IsNullOrEmpty(jwtOptions.Secret))
            {
                builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                    .AddJwtBearer(options =>
                    {
                        options.TokenValidationParameters = new TokenValidationParameters
                        {
                            ValidateIssuer = true,
                            ValidateAudience = true,
                            ValidateLifetime = true,
                            ValidateIssuerSigningKey = true,
                            ValidIssuer = jwtOptions.Issuer,
                            ValidAudience = jwtOptions.Audience,
                            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Secret)),
                            ClockSkew = TimeSpan.Zero
                        };
                        options.Events = new JwtBearerEvents
                        {
                            OnMessageReceived = context =>
                            {
                                var accessToken = context.Request.Query["access_token"];
                                var path = context.HttpContext.Request.Path;
                                // SignalR requires token in query string
                                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                                {
                                    context.Token = accessToken;
                                }
                                return Task.CompletedTask;
                            }
                        };
                    });
            }
            else
            {
                builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
            }

            builder.Services.AddAuthorization(AuthorizationPolicies.Configure);

            builder.Services.AddHttpContextAccessor();
            builder.Services.AddScoped<WebOfWork.Api.Services.ICurrentUser, WebOfWork.Api.Services.CurrentUser>();

            // ── Integrations & Services (Steps 1.14, 1.15, Phase 2A) ─────────────
            builder.Services.AddTransient<IEmailSender, SmtpEmailSender>();
            builder.Services.AddSingleton<IEmailTemplateRenderer, EmailTemplateRenderer>();

            // ── Push (FCM) ──────────────────────────────────────────────────────
            // A singleton, like IEmailSender: it holds a pooled HttpClient and a
            // cached OAuth access token, neither of which should be rebuilt per
            // notification. `AddHttpClient` is required because the sender resolves
            // its client from the factory.
            builder.Services.AddHttpClient();
            // FirebasePushSender self-disables when Firebase:ProjectId /
            // Firebase:ServiceAccountPath are missing, but registering it
            // unconditionally would leave IsConfigured=false for a service that
            // logged a warning and allocated a client. The no-op is picked here
            // instead so the notification pipeline has a single code path.
            var firebaseConfigured =
                !string.IsNullOrWhiteSpace(builder.Configuration["Firebase:ProjectId"])
                && !string.IsNullOrWhiteSpace(builder.Configuration["Firebase:ServiceAccountPath"]);
            if (firebaseConfigured)
                builder.Services.AddSingleton<IPushSender, FirebasePushSender>();
            else
            {
                builder.Logging.AddFilter("WebOfWork.Api.Integrations.Push", LogLevel.Warning);
                builder.Services.AddSingleton<IPushSender, NoOpPushSender>();
            }

            builder.Services.AddScoped<WebOfWork.Api.Services.IContentService, WebOfWork.Api.Services.ContentService>();

            // Phase 3 Identity & Auth Services
            builder.Services.AddScoped<WebOfWork.Api.Services.IPasswordHasher, WebOfWork.Api.Services.PasswordHasher>();
            builder.Services.AddScoped<WebOfWork.Api.Services.IOtpService, WebOfWork.Api.Services.OtpService>();
            builder.Services.AddScoped<WebOfWork.Api.Services.IJwtTokenService, WebOfWork.Api.Services.JwtTokenService>();
            builder.Services.AddScoped<WebOfWork.Api.Services.IRefreshTokenService, WebOfWork.Api.Services.RefreshTokenService>();
            builder.Services.AddScoped<WebOfWork.Api.Services.IAuthService, WebOfWork.Api.Services.AuthService>();
            builder.Services.AddScoped<WebOfWork.Api.Services.IPasswordResetService, WebOfWork.Api.Services.PasswordResetService>();
            builder.Services.AddScoped<WebOfWork.Api.Integrations.Google.IGoogleTokenVerifier, WebOfWork.Api.Integrations.Google.GoogleTokenVerifier>();
            builder.Services.AddScoped<WebOfWork.Api.Services.IGoogleAuthService, WebOfWork.Api.Services.GoogleAuthService>();

            // Phase 5 Services
            builder.Services.AddScoped<WebOfWork.Api.Services.IProfileService, WebOfWork.Api.Services.ProfileService>();
            builder.Services.AddScoped<WebOfWork.Api.Services.IPortfolioService, WebOfWork.Api.Services.PortfolioService>();
            builder.Services.AddScoped<WebOfWork.Api.Services.ICategoryService, WebOfWork.Api.Services.CategoryService>();
            builder.Services.AddScoped<WebOfWork.Api.Services.IFreelancerOnboardingService, WebOfWork.Api.Services.FreelancerOnboardingService>();
            builder.Services.AddScoped<WebOfWork.Api.Services.IPhoneVerificationService, WebOfWork.Api.Services.PhoneVerificationService>();
            builder.Services.AddSingleton<WebOfWork.Api.Integrations.Sms.Strategies.SmsStrategyFactory>();
            builder.Services.AddHttpClient<WebOfWork.Api.Integrations.Sms.ISmsSender, WebOfWork.Api.Integrations.Sms.AndroidSmsGatewaySmsSender>((sp, client) =>
            {
                var config = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<WebOfWork.Api.Configuration.AndroidSmsGatewayOptions>>().Value;
                var url = config.Url ?? "https://api.sms-gate.app/3rdparty/v1";
                if (!url.EndsWith("/")) url += "/";
                client.BaseAddress = new Uri(url);
                var credentials = Convert.ToBase64String(System.Text.Encoding.ASCII.GetBytes($"{config.Username}:{config.Password}"));
                client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", credentials);
            });

            builder.Services.AddValidatorsFromAssemblyContaining<WebOfWork.Api.DTOs.Platform.UpdateGlobalSettingsRequestValidator>();

            // ──────────────────────────────────────────────────────────────────────
            builder.Services.AddScoped<WebOfWork.Api.Services.IBriefService, WebOfWork.Api.Services.BriefService>();
            builder.Services.AddScoped<WebOfWork.Api.Services.IProjectService, WebOfWork.Api.Services.ProjectService>();
            builder.Services.AddScoped<WebOfWork.Api.Services.IProjectRecommendationService, WebOfWork.Api.Services.ProjectRecommendationService>();
            builder.Services.AddScoped<WebOfWork.Api.Services.IProposalService, WebOfWork.Api.Services.ProposalService>();
            builder.Services.AddScoped<WebOfWork.Api.Services.IInvitationService, WebOfWork.Api.Services.InvitationService>();
            builder.Services.AddScoped<WebOfWork.Api.Services.IContractService, WebOfWork.Api.Services.ContractService>();
            builder.Services.AddScoped<WebOfWork.Api.Services.IContractDocumentService, WebOfWork.Api.Services.ContractDocumentService>();
            builder.Services.AddScoped<WebOfWork.Api.Integrations.Cloudinary.ICloudinaryStorageService, WebOfWork.Api.Integrations.Cloudinary.CloudinaryStorageService>();
            builder.Services.AddScoped<WebOfWork.Api.Services.IDeliverableService, WebOfWork.Api.Services.DeliverableService>();
            builder.Services.AddScoped<WebOfWork.Api.Services.IRevisionService, WebOfWork.Api.Services.RevisionService>();
            builder.Services.AddScoped<WebOfWork.Api.Services.IDisputeService, WebOfWork.Api.Services.DisputeService>();
            builder.Services.AddScoped<WebOfWork.Api.Services.IReviewService, WebOfWork.Api.Services.ReviewService>();
            builder.Services.AddScoped<WebOfWork.Api.Services.IPaymentService, WebOfWork.Api.Services.PaymentService>();
            // The explicit-factory registration that used to sit here is gone.
            //
            // It shadowed the conventional registration above -- last one wins --
            // and passed `null` for `INotificationPublisher`. So in the running
            // application `PaymentService` never published a notification at all:
            // the only thing a freelancer got was the direct SMS, which ignored
            // their notification preferences. The null looked deliberate, as
            // though the publisher were optional, and it was -- the parameter is
            // nullable. That optionality is what let the omission go unnoticed.
            //
            // `INotificationPublisher` is registered below and resolves normally,
            // so the conventional registration now delivers both the in-app
            // notification and the SMS, both governed by the user's preferences.
            builder.Services.AddScoped<WebOfWork.Api.Services.ICommissionCalculator, WebOfWork.Api.Services.CommissionCalculator>();
            builder.Services.Configure<WebOfWork.Api.Configuration.PayPalOptions>(builder.Configuration.GetSection(WebOfWork.Api.Configuration.PayPalOptions.Section));
            builder.Services.AddHttpClient<WebOfWork.Api.Integrations.PayPal.IPayPalClient, WebOfWork.Api.Integrations.PayPal.PayPalClient>();
            builder.Services.AddHttpClient<WebOfWork.Api.Integrations.PayPal.IPayPalPayoutClient, WebOfWork.Api.Integrations.PayPal.PayPalPayoutClient>();
            builder.Services.AddHttpClient<WebOfWork.Api.Integrations.PayPal.IPayPalRefundClient, WebOfWork.Api.Integrations.PayPal.PayPalRefundClient>();
            builder.Services.AddHttpClient<WebOfWork.Api.Integrations.PayPal.IPayPalWebhookVerifier, WebOfWork.Api.Integrations.PayPal.PayPalWebhookVerifier>();
            builder.Services.AddScoped<WebOfWork.Api.Integrations.PayPal.IPayPalWebhookHandler, WebOfWork.Api.Integrations.PayPal.PayPalWebhookHandler>();
            builder.Services.AddHttpClient("AgenticAi", (sp, client) =>
            {
                var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<WebOfWork.Api.Configuration.AgenticAiOptions>>().Value;
                client.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/");
                client.Timeout = TimeSpan.FromSeconds(3);
            });
            builder.Services.AddScoped<WebOfWork.Api.Integrations.PayPal.IPayPalOrderVerifier, WebOfWork.Api.Integrations.PayPal.PayPalOrderVerifier>();
            builder.Services.AddScoped<WebOfWork.Api.Services.IPayoutService, WebOfWork.Api.Services.PayoutService>();
            builder.Services.AddScoped<WebOfWork.Api.Services.IRefundService, WebOfWork.Api.Services.RefundService>();
            builder.Services.AddTransient<WebOfWork.Api.Jobs.RecalculateRatingsJob>();
            builder.Services.AddTransient<WebOfWork.Api.Jobs.SendSupportReplyJob>();
            builder.Services.AddTransient<WebOfWork.Api.Jobs.SendOtpEmailJob>();
            builder.Services.AddTransient<WebOfWork.Api.Jobs.SendPasswordResetEmailJob>();
            builder.Services.AddTransient<WebOfWork.Api.Jobs.SendPhoneOtpJob>();
            builder.Services.AddTransient<WebOfWork.Api.Jobs.GenerateContractDocumentJob>();
            builder.Services.AddTransient<WebOfWork.Api.Jobs.PublishNotificationJob>();
            builder.Services.AddTransient<WebOfWork.Api.Jobs.ContractOfferNotificationJob>();
            builder.Services.AddTransient<WebOfWork.Api.Jobs.CleanupExpiredOtpsJob>();

            // Phase 14 Chat & Notifications
            builder.Services.AddScoped<WebOfWork.Api.Services.IContactInformationPolicy, WebOfWork.Api.Services.ContactInformationPolicy>();
            builder.Services.AddScoped<WebOfWork.Api.Services.IChatService, WebOfWork.Api.Services.ChatService>();
            builder.Services.AddScoped<WebOfWork.Api.Services.INotificationService, WebOfWork.Api.Services.NotificationService>();
            // Resolved from the scoped INotificationService instance below, so the
            // publish path and the preference read share one DbContext scope and
            // see the same uncommitted state.
            builder.Services.AddScoped<WebOfWork.Api.Services.INotificationPreferenceService,
                                       WebOfWork.Api.Services.NotificationPreferenceService>();
            builder.Services.AddScoped<WebOfWork.Api.Services.INotificationPublisher>(sp =>
                sp.GetRequiredService<WebOfWork.Api.Services.INotificationService>() as WebOfWork.Api.Services.NotificationService
                ?? throw new InvalidOperationException("Notification service not registered correctly."));
            builder.Services.AddSingleton<WebOfWork.Api.Integrations.Sms.Strategies.SmsStrategyFactory>(); // re-register to guarantee DI order

            // Phase 15 Support & Content
            builder.Services.AddSingleton<WebSocketGroupRegistry>();
            builder.Services.AddScoped<WebOfWork.Api.Services.ISupportService, WebOfWork.Api.Services.SupportService>();

            // Phase 16 Admin
            builder.Services.AddScoped<WebOfWork.Api.Services.IAdminUserService, WebOfWork.Api.Services.AdminUserService>();
            builder.Services.AddScoped<WebOfWork.Api.Services.IAdminAnalyticsService, WebOfWork.Api.Services.AdminAnalyticsService>();

            // Phase 18A AI workflow backend
            builder.Services.AddScoped<WebOfWork.Api.Services.IAiWorkflowService, WebOfWork.Api.Services.AiWorkflowService>();
            // Starts `screen` and `validate` runs from the proposal and payment
            // paths. Without it those two stages were unreachable, which also
            // made the manager approval gate unreachable -- a run only enters
            // `PendingManagerApproval` from a validate-stage run.
            builder.Services.AddScoped<WebOfWork.Api.Services.IAiWorkflowTrigger, WebOfWork.Api.Services.AiWorkflowTrigger>();
        // Stage resolution. Scoped, not singleton, because it reads through the
        // request's DbContext. Only the interface is registered, and every
        // consumer takes the interface -- registering the concrete type as well
        // would hand out a second, distinct instance.
        builder.Services.AddScoped<WebOfWork.Api.Services.IStageResolver, WebOfWork.Api.Services.StageResolver>();

            // Phase 18B internal read projections
            builder.Services.AddScoped<WebOfWork.Api.Services.IInternalReadService, WebOfWork.Api.Services.InternalReadService>();
            builder.Services.AddSingleton<WebOfWork.Api.Services.IInternalReadAuditService, WebOfWork.Api.Services.InternalReadAuditService>();

            var app = builder.Build();

            using (var scope = app.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<WebOfWork.Api.Data.WebOfWorkDbContext>();
                db.Database.Migrate();
            }

            // ── Locked Middleware Pipeline (Plan §7) ─────────────────────────────
            app.UseExceptionHandlingMiddleware();
            app.UseWebSockets();
            app.UseMiddleware<WebSocketBridgeMiddleware>();
            app.UseMiddleware<WebOfWork.Api.Middleware.JwtQueryStringMiddleware>();
            app.UseForwardedHeaders();
            // The dev Kestrel certificate is self-signed, so the Flutter app on a
            // physical device talks plain HTTP over the LAN. Unconditional
            // UseHttpsRedirection answered those requests with a 307 to an
            // https:// origin the device then refused. Opt out when
            // AllowHttpForDeviceClients is set, which is Development-only.
            if (!builder.Configuration.GetValue<bool>("AllowHttpForDeviceClients"))
                app.UseHttpsRedirection();
            app.UseCorrelationIdMiddleware();
            app.UseRequestLoggingMiddleware();
            app.UseInternalApiKeyMiddleware(); // MUST BE BEFORE UseRouting

            app.UseRouting();
            app.UseRateLimiter();
            app.UseCors();
            app.UseAuthentication();
            app.UseAuthorization();

            // ── Endpoints ────────────────────────────────────────────────────────
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.MapControllers();
            app.MapHealthChecks("/health");

            app.MapHub<ChatHub>("/hubs/chat");
            app.MapHub<NotificationHub>("/hubs/notifications");

            app.UseHangfireDashboardSecured();

            // ── Background Jobs ──────────────────────────────────────────────────
            RecurringJob.AddOrUpdate<WebOfWork.Api.Jobs.RecalculateRatingsJob>(
                "recalculate-ratings",
                job => job.ExecuteAsync(CancellationToken.None),
                Cron.Hourly);

            RecurringJob.AddOrUpdate<WebOfWork.Api.Jobs.CleanupExpiredOtpsJob>(
                "cleanup-expired-otps",
                job => job.ExecuteAsync(CancellationToken.None),
                Cron.Hourly);

            app.Run();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Application terminated unexpectedly");

            // Non-zero exit code, so an orchestrator sees the failure.
            //
            // Without this the process returned 0 after a fatal startup error,
            // which reads to Docker, a service manager or a CI step as a clean
            // shutdown. A container configured to restart on exit would then
            // restart-loop forever while reporting success, and the actual cause
            // is only in the logs of the previous container.
            Environment.ExitCode = 1;
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }
}







