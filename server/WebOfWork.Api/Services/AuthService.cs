using Hangfire;
using WebOfWork.Api.Data;
using WebOfWork.Api.Models;
using WebOfWork.Api.Models.Enums;
using WebOfWork.Api.Integrations.Email;
using WebOfWork.Api.Jobs;
using Microsoft.EntityFrameworkCore;

namespace WebOfWork.Api.Services;

public interface IAuthService
{
    Task SendOtpAsync(string email);
    Task<User> VerifyOtpAndCreateUserAsync(string email, string otp, string password, string fullName, UserRole role);
    Task<User> LoginAsync(string email, string password);
    Task<User?> GetUserByRefreshTokenAsync(string refreshToken);
    Task UpdateRefreshTokenAsync(Guid userId, string token, DateTime expiry);
    Task ChangePasswordAsync(Guid userId, string currentPassword, string newPassword);
    Task RequestDeleteOtpAsync(Guid userId);
    Task DeleteAccountAsync(Guid userId, string otp);
    Task<User> UpdateMeAsync(Guid userId, string? fullName, string? email);
    Task<bool> VerifyEmailChangeAsync(Guid userId, string otp);
}

public class AuthService : IAuthService
{
    private readonly WebOfWorkDbContext _db;
    private readonly IOtpService _otp;
    private readonly IPasswordHasher _hasher;
    private readonly IEmailSender _emailSender;
    private readonly IBackgroundJobClient? _jobs;

    public AuthService(WebOfWorkDbContext db, IOtpService otp, IPasswordHasher hasher, IEmailSender emailSender, IBackgroundJobClient? jobs = null)
    {
        _db = db; _otp = otp; _hasher = hasher; _emailSender = emailSender; _jobs = jobs;
    }

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
}
