using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using WebOfWork.Api.Integrations.Push;
using Xunit;

namespace WebOfWork.Api.Tests.Hardening;

/// <summary>
/// Proves the FCM service-account credential is actually usable, rather than
/// merely parseable.
///
/// This matters because the failure mode is invisible: with a config key present
/// but an unreadable or unauthorised credential, <see cref="FirebasePushSender"/>
/// disables itself, every publish silently skips the push, the device token
/// registers successfully, and the only symptom is that no notification ever
/// arrives. A test that only asserted "the JSON parsed" would pass while push was
/// completely dead.
///
/// The live test is opt-in. It needs a real network call and a real credential,
/// so it is gated on an environment variable and skipped in normal CI:
///
///   FIREBASE_SERVICE_ACCOUNT=C:\devkeys\webofwork-firebase-serviceaccount.json \
///   FIREBASE_PROJECT_ID=webofwork-ba52d \
///   dotnet test --filter FirebaseCredential
/// </summary>
public class FirebaseCredentialTests
{
    private const string AccountVar = "FIREBASE_SERVICE_ACCOUNT";
    private const string ProjectVar = "FIREBASE_PROJECT_ID";

    private static (IConfiguration Config, bool Skip) ConfigFromEnvironment()
    {
        var path = Environment.GetEnvironmentVariable(AccountVar);
        var project = Environment.GetEnvironmentVariable(ProjectVar);

        if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(project))
            return (null!, true);

        if (!File.Exists(path))
            throw new FileNotFoundException(
                $"{AccountVar} points at a file that does not exist: {path}");

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Firebase:ProjectId"] = project,
                ["Firebase:ServiceAccountPath"] = path,
            })
            .Build();

        return (config, false);
    }

    private static FirebasePushSender BuildSender(IConfiguration config)
    {
        var services = new ServiceCollection();
        services.AddHttpClient();
        return new FirebasePushSender(
            config,
            services.BuildServiceProvider().GetRequiredService<IHttpClientFactory>(),
            NullLogger<FirebasePushSender>.Instance);
    }

    /// <summary>
    /// Offline: a well-formed service account must switch the sender on. Cheap, and
    /// it catches the most common mistake -- a config key that points at nothing.
    /// </summary>
    [Fact]
    public void Sender_IsConfigured_WhenProjectAndServiceAccountAreBothPresent()
    {
        var (config, skip) = ConfigFromEnvironment();
        if (skip) return; // no credential available; the live test below is the gate

        using var sender = BuildSender(config);
        sender.IsConfigured.Should().BeTrue(
            "a readable service-account file plus a project id is all the sender needs to attempt delivery");
    }

    /// <summary>
    /// Offline: a missing project id must leave push off rather than throwing at
    /// startup. A throw here would take the whole API down for a missing optional
    /// integration.
    /// </summary>
    [Fact]
    public void Sender_SelfDisables_WhenProjectIdIsMissing()
    {
        var path = Environment.GetEnvironmentVariable(AccountVar);
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                // Deliberately no Firebase:ProjectId.
                ["Firebase:ServiceAccountPath"] = path,
            })
            .Build();

        using var sender = BuildSender(config);
        sender.IsConfigured.Should().BeFalse();
    }

    /// <summary>
    /// Live: mints a real access token and calls FCM with a deliberately invalid
    /// device token.
    ///
    /// The assertion is about *which kind* of failure comes back:
    ///   * an auth failure (401 / "SERVICE_ACCOUNT" / "PERMISSION_DENIED" /
    ///     "Request had invalid authentication credentials") means the credential
    ///     is wrong or lacks FCM permission -- push is dead.
    ///   * a token-level failure ("UNREGISTERED", "INVALID_ARGUMENT", 404) means
    ///     Google accepted our authentication and rejected only the fake token --
    ///     which is exactly what a healthy setup should do.
    ///
    /// Using a fake token is deliberate: it is the only way to reach FCM without
    /// a real device, and it separates the two failure classes cleanly.
    /// </summary>
    [Fact]
    public async Task LiveSend_AuthenticatesWithFcm_AndFailsOnlyOnTheFakeToken()
    {
        var (config, skip) = ConfigFromEnvironment();
        if (skip) return;

        using var sender = BuildSender(config);
        sender.IsConfigured.Should().BeTrue("the credential must load before we can test it");

        var results = await sender.SendAsync(new[]
        {
            new PushMessage(
                Token: "clearly-not-a-real-registration-token",
                Title: "connectivity probe",
                Body: "sent by the FirebaseCredentialTests suite",
                Data: new Dictionary<string, string> { ["type"] = "connectivity_probe" },
                Route: "notifications"),
        });

        results.Should().HaveCount(1);
        var result = results[0];

        // Reaching here at all means the request was sent and answered, so the
        // access token was minted and accepted.
        result.Error.Should().NotBeNullOrEmpty("a fake token cannot be delivered");

        var authFailure = result.Error!.Contains("UNAUTHENTICATED", StringComparison.OrdinalIgnoreCase)
            || result.Error.Contains("SERVICE_ACCOUNT", StringComparison.OrdinalIgnoreCase)
            || result.Error.Contains("PERMISSION_DENIED", StringComparison.OrdinalIgnoreCase)
            || result.Error.Contains("invalid authentication credentials", StringComparison.OrdinalIgnoreCase)
            || result.Error.Contains("403", StringComparison.Ordinal)
            || result.Error.Contains("401", StringComparison.Ordinal);

        authFailure.Should().BeFalse(
            $"FCM rejected our credentials, so no push could ever be delivered. Response: {result.Error}");

        result.Delivered.Should().BeFalse("the token is fake by construction");
    }

    /// <summary>
    /// Offline: an unconfigured sender must report itself unconfigured and return
    /// no results rather than attempting delivery. This is the <c>NoOpPushSender</c>
    /// contract, and the notification pipeline depends on it -- a second code path
    /// with a null guard would be a branch that never runs in development.
    /// </summary>
    [Fact]
    public async Task NoOpSender_ReportsUnconfiguredAndDeliversNothing()
    {
        IPushSender sender = new NoOpPushSender();

        sender.IsConfigured.Should().BeFalse();

        var results = await sender.SendAsync(new[]
        {
            new PushMessage("any-token", "t", "b"),
        });

        results.Should().HaveCount(1);
        results[0].Delivered.Should().BeFalse();
        results[0].Error.Should().Be("push not configured");
    }
}
