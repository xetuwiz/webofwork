using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Xunit;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using StackExchange.Redis;

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

// A bare WebApplicationFactory<Program> cannot build a host for this API.
//
// Program.cs registers IConnectionMultiplexer only when Database:Redis is
// non-empty, OtpService depends on IConnectionMultiplexer unconditionally, and
// ValidateOnBuild then fails before a single request is served -- the symptom
// being "the entry point exited without ever building an IHost".
//
// Nothing in these three tests touches Redis: they assert that a non-admin is
// refused a settings write. So the fixture supplies a stub multiplexer rather
// than weakening the check in Program.cs for a test that does not care about
// OTP, and rather than standing up a Redis container -- which would have made
// the no-container tier require Docker to exist, for no gain.
public sealed class RedisStubbedWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<IConnectionMultiplexer>(Mock.Of<IConnectionMultiplexer>());
        });
    }
}

public class RoleAuthorizationTests : IClassFixture<RedisStubbedWebApplicationFactory>
{
    private readonly WebApplicationFactory<Program> _factory;

    public RoleAuthorizationTests(RedisStubbedWebApplicationFactory factory)
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
