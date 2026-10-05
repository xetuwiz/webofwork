using Xunit;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using WebOfWork.Api.Data;
using WebOfWork.Api.Models.Enums;
using WebOfWork.Api.Services;
using WebOfWork.Api.Integrations.Google;
using WebOfWork.Api.Configuration;
using Microsoft.Extensions.Options;
using System.Threading.Tasks;
using System.IO;
using Google.Apis.Auth.OAuth2;
using Testcontainers.PostgreSql;

namespace WebOfWork.Api.Tests.Auth;

[Trait("Category", "Container")]
public class RealGoogleAuthE2ETests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:15-alpine")
        .Build();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
    }

    public async Task DisposeAsync()
    {
        await _postgres.DisposeAsync();
    }

    [Fact]
    public async Task AuthenticateGoogleUser_ShouldVerifyRealGoogleSignature_AndCreateUser()
    {
        // 1. SETUP REAL DATABASE
        var options = new DbContextOptionsBuilder<WebOfWorkDbContext>()
            .UseSnakeCaseNamingConvention().UseNpgsql(_postgres.GetConnectionString())
            .Options;
        
        await using var db = new WebOfWorkDbContext(options);
        await db.Database.EnsureCreatedAsync();

        // 2. CONFIGURE REAL GOOGLE VERIFIER
        var clientId = "461728733485-3if1cqp7j9e12lkl7m5ha4lgrmgkuvt8.apps.googleusercontent.com";
        var googleOptions = Options.Create(new GoogleOptions { ClientId = clientId });
        var verifier = new GoogleTokenVerifier(googleOptions);
        
        var service = new GoogleAuthService(verifier, db);

        // 3. MINT A REAL OIDC TOKEN USING THE SERVICE ACCOUNT
        var keyPath = @"C:\catacolite\webofwork\google-test-key.json";
        
        var credential = GoogleCredential.FromFile(keyPath);
        var oidcToken = await credential.GetOidcTokenAsync(OidcTokenOptions.FromTargetAudience(clientId));
        var idToken = await oidcToken.GetAccessTokenAsync();

        // 4. ACT: Authenticate via the actual backend service
        var user = await service.AuthenticateGoogleUserAsync(idToken, UserRole.Freelancer);
        
        // 5. ASSERT
        user.Should().NotBeNull();
        user.IsGoogleAccount.Should().BeTrue();
        user.Email.Should().EndWith("@webofwork.iam.gserviceaccount.com");
        user.GoogleSub.Should().NotBeNullOrEmpty("Google successfully provided the unique Subject ID");
    }
}
