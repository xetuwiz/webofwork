using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace WebOfWork.Api.Tests.Platform;

/// <summary>
/// Does the containerised AI service point at a port the API actually serves
/// plaintext on?
/// </summary>
/// <remarks>
/// <para>
/// The compose file had <c>DOTNET_BASE_URL: http://host.docker.internal:5001</c>.
/// <c>launchSettings.json</c> binds four endpoints:
/// </para>
/// <code>
/// applicationUrl: "https://0.0.0.0:5001;http://0.0.0.0:5000;
///                 https://localhost:5001;http://localhost:5000"
/// </code>
/// <para>
/// 5001 is HTTPS and 5000 is HTTP. A plain <c>http://</c> URL aimed at 5001 asks
/// for TLS on a plaintext socket, so every call out of the container failed at the
/// handshake -- and surfaced as <c>DotNetUnavailableError</c>, which the dispatcher
/// reports as "Could not reach the WebOfWork API". A stage that looks like a dead
/// network because of one mismatched digit.
/// </para>
/// <para>
/// This exists because the E2E tests that would have caught it need Docker
/// Desktop, so on a machine without it the failure is silent until someone runs
/// the stack by hand. It is a configuration test, not a behaviour test: it cannot
/// prove the container can reach the API, only that the URL is self-consistent
/// with the scheme the API serves on that port.
/// </para>
/// </remarks>
public sealed class DockerComposeApiUrlTests
{
    [Fact]
    public void The_ai_service_points_at_a_plaintext_port()
    {
        var url = DotNetBaseUrl();

        // The load-bearing assertion. An http:// URL against the HTTPS port is the
        // exact defect, and it is invisible until the container runs.
        url.Scheme.Should().Be(
            "http",
            "the API's TLS is self-signed and DOTNET_VERIFY_TLS is false, so the "
            + "plaintext endpoint is the one meant to be used here");

        url.Port.Should().Be(
            PlaintextApiPort(),
            "launchSettings binds HTTP on a different port from HTTPS, and pointing "
            + "an http:// URL at the HTTPS port fails at the handshake");
    }

    [Fact]
    public void The_ai_service_does_not_reach_the_api_by_container_localhost()
    {
        // From inside a container, localhost is the container.
        DotNetBaseUrl().Host.Should().NotBe(
            "localhost",
            "\"localhost\" inside the container is the container itself, so every "
            + "call would fail to connect rather than reach the host");
    }

    [Fact]
    public void The_ai_service_reaches_the_host_rather_than_another_container()
    {
        // The failure mode here is quieter than localhost: it resolves fine and
        // connects to the wrong thing.
        var host = DotNetBaseUrl().Host;
        host.Should().BeOneOf("host.docker.internal", "host.containers.internal");
    }

    /// <summary>
    /// The port the API serves plaintext on, read from launchSettings.
    /// </summary>
    /// <remarks>
    /// Read rather than hardcoded, so re-binding the API does not silently turn
    /// this test into an assertion about a port nothing uses. If nothing plaintext
    /// is bound the test fails by returning null, which is the correct outcome --
    /// there would be no valid value for compose to point at.
    /// </remarks>
    private static int PlaintextApiPort()
    {
        var launchSettings = File.ReadAllText(RepoFile(
            "server", "WebOfWork.Api", "Properties", "launchSettings.json"));

        var match = Regex.Match(launchSettings, "http://[^\";]*?:(\\d+)");
        match.Success.Should().BeTrue(
            "launchSettings.json should bind at least one plaintext endpoint");

        return int.Parse(match.Groups[1].Value);
    }

    private static Uri DotNetBaseUrl()
    {
        var compose = File.ReadAllText(RepoFile("docker-compose.yml"));

        var match = Regex.Match(compose, "DOTNET_BASE_URL:\\s*(\\S+)");
        match.Success.Should().BeTrue(
            "docker-compose.yml should set DOTNET_BASE_URL for the ai service");

        return new Uri(match.Groups[1].Value);
    }

    private static string RepoFile(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null
               && !File.Exists(Path.Combine(directory.FullName, "WebOfWork.sln")))
        {
            directory = directory.Parent;
        }

        directory.Should().NotBeNull("the solution file marks the repository root");
        return Path.Combine(new[] { directory!.FullName }.Concat(parts).ToArray());
    }
}