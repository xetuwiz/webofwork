using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Xunit;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Search;
using MailKit.Security;
using StackExchange.Redis;
using WebOfWork.Api.Data;
using WebOfWork.Api.Models.Enums;
using WebOfWork.Api.Services;
using WebOfWork.Api.Integrations.Email;
using WebOfWork.Api.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging.Abstractions;

namespace WebOfWork.Api.Tests.Auth;

// We implement IAsyncLifetime to manage the Docker containers
public class RealAuthE2ETests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:15-alpine")
        .Build();

    private readonly RedisContainer _redis = new RedisBuilder()
        .WithImage("redis:7-alpine")
        .Build();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        await _redis.StartAsync();
    }

    public async Task DisposeAsync()
    {
        await _postgres.DisposeAsync();
        await _redis.DisposeAsync();
    }

    [Fact(Timeout = 60000)] // 60s timeout to allow for network/email propagation
    public async Task CompleteE2E_RegistrationFlow_SendsRealEmail_ChecksImap_CreatesUserInDocker()
    {
        // 1. SETUP REAL DATABASE
        var options = new DbContextOptionsBuilder<WebOfWorkDbContext>()
            .UseSnakeCaseNamingConvention().UseNpgsql(_postgres.GetConnectionString())
            .Options;
        
        await using var db = new WebOfWorkDbContext(options);
        await db.Database.EnsureCreatedAsync();

        // 2. SETUP REAL REDIS
        var redisMultiplexer = await ConnectionMultiplexer.ConnectAsync(_redis.GetConnectionString());
        var otpService = new OtpService(redisMultiplexer);

        // 3. SETUP REAL EMAIL SENDER
        var mailOptions = Options.Create(new MailOptions {
            Host = "mail.xetuwiz.de",
            Port = 587,
            Username = "no-reply@xetuwiz.de",
            Password = "Noreplypassword1#",
            FromAddress = "no-reply@xetuwiz.de",
            FromName = "WebOfWork Tests"
        });
        var emailSender = new SmtpEmailSender(mailOptions, NullLogger<SmtpEmailSender>.Instance);

        // 4. SETUP HASHER & TARGET SERVICE
        var hasher = new PasswordHasher();
        // `redisMultiplexer` (not the local `redis` name) because this file kept
        // only the built `otpService`; the multiplexer it wraps is the one
        // AuthService now needs for its own redis-backed throttling.
        var authService = new AuthService(db, otpService, hasher, emailSender, new RefreshTokenService(), redisMultiplexer);
        
        var testEmail = "test@xetuwiz.de";

        // ==========================================
        // ACT 1: Send OTP
        // ==========================================
        await authService.SendOtpAsync(testEmail);

        // ==========================================
        // ACT 2: Extract OTP via IMAP using MailKit
        // ==========================================
        using var imapClient = new ImapClient();
        await imapClient.ConnectAsync("mail.xetuwiz.de", 993, SecureSocketOptions.SslOnConnect);
        await imapClient.AuthenticateAsync("test@xetuwiz.de", "Testpassword1#");
        
        var inbox = imapClient.Inbox;
        await inbox.OpenAsync(FolderAccess.ReadWrite);

        // Search for unread emails sent to our specific test alias, polling up to 3 times (15 seconds)
        IList<UniqueId> uids = new List<UniqueId>();
        for (int i = 0; i < 3; i++)
        {
            await Task.Delay(5000); // Wait 5s before checking
            uids = await inbox.SearchAsync(SearchQuery.ToContains(testEmail));
            if (uids.Any()) break;
        }
        
        uids.Should().NotBeEmpty("The OTP email should have arrived in the inbox within 15 seconds");

        var message = await inbox.GetMessageAsync(uids.Last());
        
        // Extract the 6 digit code using Regex from the HTML body
        var match = Regex.Match(message.HtmlBody, @"<strong>(\d{6})</strong>");
        match.Success.Should().BeTrue();
        var realOtpCode = match.Groups[1].Value;

        // Cleanup skipped to avoid compatibility issues with different MailKit versions
        await imapClient.DisconnectAsync(true);

        // ==========================================
        // ACT 3: Verify OTP and Login
        // ==========================================
        var user = await authService.VerifyOtpAndCreateUserAsync(
            testEmail, 
            realOtpCode, 
            "SecurePass123!", 
            "E2E Integration User", 
            UserRole.Client
        );

        // Assertions
        user.Should().NotBeNull();
        user.Email.Should().Be(testEmail);
        user.IsEmailVerified.Should().BeTrue();
        user.PasswordHash.Should().NotBeNullOrEmpty();
        hasher.VerifyPassword("SecurePass123!", user.PasswordHash).Should().BeTrue();
        
        // Verify Redis cleaned up the OTP
        var lingeringOtp = await otpService.GetOtpAsync($"reg:{testEmail}");
        lingeringOtp.Should().BeNull();
    }
}
