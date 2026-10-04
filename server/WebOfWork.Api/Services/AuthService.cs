using Hangfire;
using WebOfWork.Api.Data;
using WebOfWork.Api.Models;
using WebOfWork.Api.Models.Enums;
using WebOfWork.Api.Integrations.Email;
using WebOfWork.Api.Jobs;
using WebOfWork.Api.DTOs.Auth;
using StackExchange.Redis;
using Microsoft.EntityFrameworkCore;

namespace WebOfWork.Api.Services;

public interface IAuthService
{
    Task SendOtpAsync(string email);
    /// <summary>Soft-delete aware existence check used to reject duplicate signups at send-otp time.</summary>
    Task<bool> EmailExistsAsync(string email);
    Task<User> VerifyOtpAndCreateUserAsync(string email, string otp, string password, string fullName, UserRole role);
    Task<User> LoginAsync(string email, string password);
    Task<User?> GetUserByRefreshTokenAsync(string refreshToken);
    Task UpdateRefreshTokenAsync(Guid userId, string token, DateTime expiry);
    Task ChangePasswordAsync(Guid userId, string currentPassword, string newPassword);
    Task RequestDeleteOtpAsync(Guid userId);
    Task DeleteAccountAsync(Guid userId, string otp);
    Task<User> UpdateMeAsync(Guid userId, string? fullName, string? email);
    Task<bool> VerifyEmailChangeAsync(Guid userId, string otp);
    Task CacheRegistrationAsync(string email, PendingRegistration registration, TimeSpan expiry);
    Task<PendingRegistration?> GetRegistrationAsync(string email);
    Task DeleteRegistrationAsync(string email);
    Task<string> SendAccountOtpAsync(string email, string purpose, TimeSpan ttl);
    Task<bool> VerifyAccountOtpAsync(string email, string purpose, string otp);
    Task<User> CreateRegistrationUserAsync(PendingRegistration registration);
    Task<User?> GetUserByIdAsync(Guid userId);
    Task<User> DeleteUserAccountAsync(Guid userId, string? password, string? otp);
    Task<string> IssueRefreshTokenAsync(User user);
    Task<User?> ValidateRefreshTokenAsync(string token);
    Task RevokeRefreshTokenAsync(string? token, CancellationToken ct = default);
}

public class AuthService : IAuthService
{
    private readonly WebOfWorkDbContext _db;
    private readonly IOtpService _otp;
    private readonly IPasswordHasher _hasher;
    private readonly IEmailSender _emailSender;
    private readonly IBackgroundJobClient? _jobs;
    private readonly IConnectionMultiplexer _redis;
    private readonly IRefreshTokenService _refreshTokens;

    public AuthService(WebOfWorkDbContext db, IOtpService otp, IPasswordHasher hasher, IEmailSender emailSender, IRefreshTokenService refreshTokens, IConnectionMultiplexer redis, IBackgroundJobClient? jobs = null)
    {
        _db = db; _otp = otp; _hasher = hasher; _emailSender = emailSender; _jobs = jobs; _redis = redis; _refreshTokens = refreshTokens;
    }

    public async Task<bool> EmailExistsAsync(string email) =>
        await _db.Users.AnyAsync(u => u.Email == email && !u.IsDeleted);

    public async Task SendOtpAsync(string email)
    {
        var existing = await _db.Users.AnyAsync(u => u.Email == email && !u.IsDeleted);
        if (existing) throw new InvalidOperationException("Email already in use.");

        var hasCooldown = await _otp.HasCooldownAsync($"reg:{email}");
        if (hasCooldown) throw new InvalidOperationException("Please wait 60 seconds before requesting another OTP.");

        var code = new Random().Next(100000, 999999).ToString();
        await _otp.SetOtpAsync($"reg:{email}", code, TimeSpan.FromMinutes(10));
        await _otp.SetCooldownAsync($"reg:{email}", TimeSpan.FromSeconds(60));

        if (_jobs is not null)
        {
            _jobs.Enqueue<SendOtpEmailJob>(job => job.ExecuteAsync(email, code, CancellationToken.None));
            return;
        }

        var html = $"<h2>Welcome to WebOfWork!</h2><p>Your verification code is: <strong>{code}</strong></p><p>This code expires in 10 minutes.</p>";
        await _emailSender.SendAsync(email, "Your WebOfWork OTP", html);
    }

    public async Task<User> VerifyOtpAndCreateUserAsync(string email, string otp, string password, string fullName, UserRole role)
    {
        var stored = await _otp.GetOtpAsync($"reg:{email}");
        if (stored != otp) throw new UnauthorizedAccessException("Invalid or expired OTP.");

        var existing = await _db.Users.AnyAsync(u => u.Email == email);
        if (existing) throw new InvalidOperationException("Email already in use.");

        var user = new User
        {
            Email = email,
            FullName = fullName,
            PasswordHash = _hasher.HashPassword(password),
            Role = role,
            CurrentRole = role,
            IsFreelancer = role == UserRole.Freelancer,
            IsEmailVerified = true,
            Status = UserStatus.Active
        };
        _db.Users.Add(user);

        // Phase 5: Auto-create profiles
        if (role == UserRole.Client)
        {
            _db.ClientProfiles.Add(new ClientProfile { User = user });
        }
        else if (role == UserRole.Freelancer)
        {
            _db.FreelancerProfiles.Add(new FreelancerProfile { User = user, IsActive = false, Availability = true });
        }
        await _db.SaveChangesAsync();
        await _otp.DeleteOtpAsync($"reg:{email}");
        return user;
    }

    public async Task<User> LoginAsync(string email, string password)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email);
        if (user == null || user.IsDeleted || user.Status == UserStatus.Deleted) throw new UnauthorizedAccessException("Invalid credentials.");
        if (user.Status == UserStatus.Blocked) throw new UnauthorizedAccessException("Account blocked.");
        if (user.PasswordHash == null || !_hasher.VerifyPassword(password, user.PasswordHash)) throw new UnauthorizedAccessException("Invalid credentials.");
        return user;
    }

    public async Task UpdateRefreshTokenAsync(Guid userId, string token, DateTime expiry)
    {
        var user = await _db.Users.FindAsync(userId);
        if (user != null)
        {
            user.RefreshToken = token;
            user.RefreshTokenExpiry = expiry;
            await _db.SaveChangesAsync();
        }
    }

    public Task<User?> GetUserByRefreshTokenAsync(string refreshToken)
    {
        return _db.Users.FirstOrDefaultAsync(user =>
            user.RefreshToken == refreshToken &&
            user.RefreshTokenExpiry.HasValue &&
            user.RefreshTokenExpiry.Value > DateTime.UtcNow &&
            !user.IsDeleted &&
            user.Status == UserStatus.Active);
    }

    public async Task ChangePasswordAsync(Guid userId, string currentPassword, string newPassword)
    {
        var user = await _db.Users.FindAsync(userId);
        if (user == null || user.PasswordHash == null) throw new UnauthorizedAccessException("User not found or uses external login.");

        if (!_hasher.VerifyPassword(currentPassword, user.PasswordHash)) throw new UnauthorizedAccessException("Incorrect current password.");

        user.PasswordHash = _hasher.HashPassword(newPassword);
        await _db.SaveChangesAsync();
    }

    public async Task RequestDeleteOtpAsync(Guid userId)
    {
        var user = await _db.Users.FindAsync(userId);
        if (user == null) return;

        var code = new Random().Next(100000, 999999).ToString();
        await _otp.SetOtpAsync($"del:{userId}", code, TimeSpan.FromMinutes(10));
        await _emailSender.SendAsync(user.Email, "Account Deletion OTP", $"<p>Your account deletion code is: <strong>{code}</strong></p>");
    }

    public async Task DeleteAccountAsync(Guid userId, string otp)
    {
        var stored = await _otp.GetOtpAsync($"del:{userId}");
        if (stored != otp) throw new UnauthorizedAccessException("Invalid or expired OTP.");

        var user = await _db.Users.FindAsync(userId);
        if (user != null)
        {
            user.IsDeleted = true;
            user.DeletedAt = DateTime.UtcNow;
            user.Status = UserStatus.Deleted;
            await _db.SaveChangesAsync();
        }
        await _otp.DeleteOtpAsync($"del:{userId}");
    }

    public async Task<User> UpdateMeAsync(Guid userId, string? fullName, string? email)
    {
        var user = await _db.Users.FindAsync(userId);
        if (user == null) throw new UnauthorizedAccessException("User not found.");

        if (!string.IsNullOrEmpty(fullName)) user.FullName = fullName;

        if (!string.IsNullOrEmpty(email) && !string.Equals(email, user.Email, StringComparison.OrdinalIgnoreCase))
        {
            var existing = await _db.Users.AnyAsync(u => u.Email == email && u.Id != userId);
            if (existing) throw new InvalidOperationException("Email already in use.");

            var code = new Random().Next(100000, 999999).ToString();
            await _otp.SetOtpAsync($"email:{userId}", code, TimeSpan.FromMinutes(10));
            user.PendingEmail = email;
            user.EmailOtp = code;
            user.IsEmailVerified = false;
        }

        await _db.SaveChangesAsync();
        if (user.PendingEmail != null)
        {
            if (_jobs is not null)
                _jobs.Enqueue<SendOtpEmailJob>(job => job.ExecuteAsync(user.PendingEmail, user.EmailOtp ?? string.Empty, CancellationToken.None));
            else if (_emailSender is not null && !string.IsNullOrEmpty(user.EmailOtp))
                await _emailSender.SendAsync(user.PendingEmail, "Verify your WebOfWork email", $"<p>Your verification code is: <strong>{user.EmailOtp}</strong></p>");
        }
        return user;
    }

    public async Task<bool> VerifyEmailChangeAsync(Guid userId, string otp)
    {
        var stored = await _otp.GetOtpAsync($"email:{userId}");
        if (stored != otp) return false;
        var user = await _db.Users.FindAsync(userId);
        if (user?.PendingEmail == null) return false;
        var existing = await _db.Users.AnyAsync(u => u.Email == user.PendingEmail && u.Id != userId);
        if (existing) throw new InvalidOperationException("Email is already in use.");
        user.Email = user.PendingEmail;
        user.PendingEmail = null;
        user.EmailOtp = null;
        user.IsEmailVerified = true;
        await _db.SaveChangesAsync();
        await _otp.DeleteOtpAsync($"email:{userId}");
        return true;
    }

    public Task CacheRegistrationAsync(string email, PendingRegistration registration, TimeSpan expiry) =>
        _redis.GetDatabase().StringSetAsync($"register_temp:{email.ToLowerInvariant()}", System.Text.Json.JsonSerializer.Serialize(registration), expiry);

    public async Task<PendingRegistration?> GetRegistrationAsync(string email)
    {
        var value = await _redis.GetDatabase().StringGetAsync($"register_temp:{email.ToLowerInvariant()}");
        return value.HasValue ? System.Text.Json.JsonSerializer.Deserialize<PendingRegistration>(value.ToString()!) : null;
    }

    public Task DeleteRegistrationAsync(string email) => _redis.GetDatabase().KeyDeleteAsync($"register_temp:{email.ToLowerInvariant()}");

    public async Task<string> SendAccountOtpAsync(string email, string purpose, TimeSpan ttl)
    {
        var cooldown = await _redis.GetDatabase().StringSetAsync($"otp_cooldown:{email.ToLowerInvariant()}", "1", TimeSpan.FromSeconds(60), When.NotExists);
        if (!cooldown) throw new InvalidOperationException("Please wait before requesting another OTP.");
        var code = Random.Shared.Next(100000, 1000000).ToString();
        var db = _redis.GetDatabase();
        await db.StringSetAsync($"otp:{purpose}:{email.ToLowerInvariant()}", code, ttl);
        if (_jobs is not null) _jobs.Enqueue<SendOtpEmailJob>(job => job.ExecuteAsync(email, code, CancellationToken.None));
        else await _emailSender.SendAsync(email, "Your Verification Code", $"<p>Your verification code is <strong>{code}</strong>.</p>");
        return code;
    }

    public async Task<bool> VerifyAccountOtpAsync(string email, string purpose, string otp)
    {
        var db = _redis.GetDatabase();
        var key = $"otp:{purpose}:{email.ToLowerInvariant()}";
        var saved = await db.StringGetAsync(key);
        if (!saved.HasValue || saved != otp) return false;
        await db.KeyDeleteAsync(key);
        return true;
    }

    public async Task<User> CreateRegistrationUserAsync(PendingRegistration registration)
    {
        if (await _db.Users.AnyAsync(u => u.Email == registration.Email)) throw new InvalidOperationException("Email already exists");
        var user = new User { Email = registration.Email, FullName = registration.FullName, PasswordHash = _hasher.HashPassword(registration.Password), Role = UserRole.Client, CurrentRole = UserRole.Client, IsEmailVerified = true, Status = UserStatus.Active };
        _db.Users.Add(user);

        // Public signup is always a client, so the client profile must exist from
        // the moment the account is created. Without it every profile endpoint
        // (/api/profile/me/, /me/update/, ...) throws "Profile not found" and the
        // whole profile UI is dead for anyone who signed up through the UI.
        // VerifyOtpAndCreateUserAsync already does this for its own path.
        _db.ClientProfiles.Add(new ClientProfile { User = user });

        await _db.SaveChangesAsync();
        return user;
    }

    public Task<User?> GetUserByIdAsync(Guid userId) => _db.Users.FirstOrDefaultAsync(u => u.Id == userId);

    public async Task<User> DeleteUserAccountAsync(Guid userId, string? password, string? otp)
    {
        var user = await _db.Users.FindAsync(userId) ?? throw new KeyNotFoundException("User not found");
        if (user.IsGoogleAccount)
        {
            if (string.IsNullOrWhiteSpace(otp)) throw new InvalidOperationException("Verification code is required.");
            if (!await VerifyAccountOtpAsync(user.Email, "account_deletion", otp)) throw new InvalidOperationException("Invalid or expired verification code.");
        }
        else
        {
            if (string.IsNullOrWhiteSpace(password)) throw new InvalidOperationException("Password is required to delete your account.");
            if (user.PasswordHash == null || !_hasher.VerifyPassword(password, user.PasswordHash)) throw new InvalidOperationException("Current password is incorrect.");
        }
        user.Email = $"deleted_{Guid.NewGuid().ToString("N")[..8]}_{user.Email}";
        user.IsDeleted = true;
        user.DeletedAt = DateTime.UtcNow;
        user.Status = UserStatus.Deleted;
        user.RefreshToken = null;
        user.RefreshTokenExpiry = null;
        await _db.SaveChangesAsync();
        return user;
    }

    public async Task<string> IssueRefreshTokenAsync(User user)
    {
        var token = _refreshTokens.GenerateRefreshToken();
        user.RefreshToken = token;
        user.RefreshTokenExpiry = DateTime.UtcNow.AddDays(7);
        await _db.SaveChangesAsync();
        return token;
    }

    /// <summary>
    /// Resolves a refresh token to a user who may still be issued access tokens.
    /// </summary>
    /// <remarks>
    /// The <c>Status == Active</c> clause is what makes admin moderation stick.
    /// Without it, blocking a user only closed the front door: the refresh token
    /// stayed valid in the database for its full 7-day sliding expiry, so
    /// <c>POST /auth/refresh</c> happily minted a fresh access token every 60
    /// minutes for an account the admin had just blocked. Login checked the
    /// status; refresh did not, and refresh is the long-lived path.
    ///
    /// <c>GetUserByRefreshTokenAsync</c> immediately below already had this check
    /// and has no callers, which is the shape of the bug: the correct
    /// implementation existed and the wrong one was wired up.
    /// </remarks>
    public Task<User?> ValidateRefreshTokenAsync(string token) =>
        _db.Users.FirstOrDefaultAsync(u =>
            u.RefreshToken == token &&
            u.RefreshTokenExpiry > DateTime.UtcNow &&
            !u.IsDeleted &&
            u.Status == UserStatus.Active);

    /// <summary>
    /// Invalidates a refresh token server-side.
    /// </summary>
    /// <remarks>
    /// Logout used to delete the browser cookie and nothing else, so the token
    /// itself stayed valid in the database. Any copy taken from a login or refresh
    /// response body -- which native clients receive, and which the web client also
    /// gets in the body -- survived logout for up to 7 days.
    ///
    /// Keyed on the token rather than the user id because logout is
    /// <c>[AllowAnonymous]</c>: the caller may not be authenticated, and all it can
    /// be assumed to hold is the token itself. A no-op for an unknown or already
    /// revoked token, because logout must succeed whether or not there was
    /// anything to revoke.
    /// </remarks>
    public async Task RevokeRefreshTokenAsync(string? token, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(token)) return;

        var user = await _db.Users.FirstOrDefaultAsync(u => u.RefreshToken == token, ct);
        if (user is null) return;

        user.RefreshToken = null;
        user.RefreshTokenExpiry = null;
        await _db.SaveChangesAsync(ct);
    }

}