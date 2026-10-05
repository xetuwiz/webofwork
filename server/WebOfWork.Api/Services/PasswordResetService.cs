using Hangfire;
using WebOfWork.Api.Data;
using WebOfWork.Api.Integrations.Email;
using WebOfWork.Api.Jobs;
using Microsoft.EntityFrameworkCore;
using WebOfWork.Api.DTOs.Auth;
using StackExchange.Redis;

namespace WebOfWork.Api.Services;

public interface IPasswordResetService
{
    Task<string?> CreateResetGrantAsync(string email);
    Task<ResetGrant?> ValidateResetTokenAsync(string uid, string token);
    Task ResetPasswordAsync(string uid, string token, string newPassword);
}

public class PasswordResetService : IPasswordResetService
{
    private readonly WebOfWorkDbContext _db;
    private readonly IOtpService _otpService;
    private readonly IPasswordHasher _hasher;
    private readonly IEmailSender _emailSender;
    private readonly IBackgroundJobClient? _jobs;
    private readonly IConnectionMultiplexer _redis;

    public PasswordResetService(WebOfWorkDbContext db, IOtpService otpService, IPasswordHasher hasher, IEmailSender emailSender, IConnectionMultiplexer redis, IBackgroundJobClient? jobs = null)
    {
        _db = db; _otpService = otpService; _hasher = hasher; _emailSender = emailSender; _jobs = jobs; _redis = redis;
    }

    public async Task<string?> CreateResetGrantAsync(string email)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email && !u.IsDeleted);
        if (user == null) return null;
        var uid = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(user.Id.ToString())).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var token = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
        var db = _redis.GetDatabase();
        await db.StringSetAsync($"reset:{uid}:{token}", user.Id.ToString(), TimeSpan.FromHours(24));
        if (_jobs is not null) _jobs.Enqueue<SendPasswordResetEmailJob>(job => job.ExecuteAsync(email, $"{uid}:{token}", CancellationToken.None));
        else await _emailSender.SendAsync(email, "Reset your password", $"<p>Password reset token: <strong>{uid}:{token}</strong></p>");
        return $"{uid}:{token}";
    }

    public async Task<ResetGrant?> ValidateResetTokenAsync(string uid, string token)
    {
        var value = await _redis.GetDatabase().StringGetAsync($"reset:{uid}:{token}");
        if (!value.HasValue || !Guid.TryParse(value.ToString(), out var userId)) return null;
        var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId && !u.IsDeleted);
        return user == null ? null : new ResetGrant(user.Id.ToString(), user.Email);
    }

    public async Task ResetPasswordAsync(string uid, string token, string newPassword)
    {
        var grant = await ValidateResetTokenAsync(uid, token) ?? throw new UnauthorizedAccessException("Reset link is invalid or expired");
        var user = await _db.Users.FindAsync(Guid.Parse(grant.UserId)) ?? throw new UnauthorizedAccessException("Invalid reset link");
        user.PasswordHash = _hasher.HashPassword(newPassword);
        await _db.SaveChangesAsync();
        await _redis.GetDatabase().KeyDeleteAsync($"reset:{uid}:{token}");
    }
}
