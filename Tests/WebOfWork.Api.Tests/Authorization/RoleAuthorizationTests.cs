using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Xunit;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.Authentication;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace WebOfWork.Api.Tests.Authorization;

public class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public TestAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, 
        ILoggerFactory logger, UrlEncoder encoder)
        : base(options, logger, encoder) { }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (Context.Request.Headers.TryGetValue("X-Test-Role", out var role))
        {
            var claims = new[] { 
                new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
                new Claim("role", role.ToString())
            };
            var identity = new ClaimsIdentity(claims, "Test", ClaimTypes.NameIdentifier, "role");
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, "TestScheme");
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }

        return Task.FromResult(AuthenticateResult.NoResult());
    }
}

public class RoleAuthorizationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public RoleAuthorizationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.AddAuthentication(defaultScheme: "TestScheme")
                    .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("TestScheme", options => { });
            });
        });
    }

    [Theory]
    [InlineData("Admin", HttpStatusCode.OK)]
    [InlineData("Client", HttpStatusCode.Forbidden)]
    [InlineData("Freelancer", HttpStatusCode.Forbidden)]
    public async Task GlobalSettingsUpdate_RequiresAdminRole(string role, HttpStatusCode expectedStatus)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Role", role);

        var content = new StringContent("{\"commissionPercentage\":12}", System.Text.Encoding.UTF8, "application/json");
        var response = await client.PatchAsync("/api/admin/settings/global", content);
        
        response.StatusCode.Should().Be(expectedStatus);
    }
}
