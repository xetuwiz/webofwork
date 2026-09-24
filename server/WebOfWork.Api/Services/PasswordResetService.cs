using Hangfire;
using WebOfWork.Api.Data;
using WebOfWork.Api.Integrations.Email;
using WebOfWork.Api.Jobs;
using Microsoft.EntityFrameworkCore;

namespace WebOfWork.Api.Services;

public interface IPasswordResetService
{
    Task GenerateResetTokenAsync(string email);
    Task<bool> ValidateResetTokenAsync(string email, string token);
    Task ResetPasswordAsync(string email, string token, string newPassword);
}

public class PasswordResetService : IPasswordResetService
{
    private readonly WebOfWorkDbContext _db;
    private readonly IOtpService _otpService;
    private readonly IPasswordHasher _hasher;
    private readonly IEmailSender _emailSender;
    private readonly IBackgroundJobClient? _jobs;

    public PasswordResetService(WebOfWorkDbContext db, IOtpService otpService, IPasswordHasher hasher, IEmailSender emailSender, IBackgroundJobClient? jobs = null)
    {
        _db = db; _otpService = otpService; _hasher = hasher; _emailSender = emailSender; _jobs = jobs;
    }

    public async Task GenerateResetTokenAsync(string email)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email && !u.IsDeleted);
        if (user == null) return; // Silent failure for security

        var token = Guid.NewGuid().ToString("N");
        await _otpService.SetOtpAsync($"reset:{email}", token, TimeSpan.FromMinutes(15));

        if (_jobs is not null)
        {
            _jobs.Enqueue<SendPasswordResetEmailJob>(job => job.ExecuteAsync(email, token, CancellationToken.None));
            return;
        }

        var html = $"<h2>Password Reset</h2><p>Use the following token to reset your password:</p><p><strong>{token}</strong></p>";
        await _emailSender.SendAsync(email, "Password Reset Request", html);
    }

    public async Task ResetPasswordAsync(string email, string token, string newPassword)
    {
        var stored = await _otpService.GetOtpAsync($"reset:{email}");
        if (stored != token) throw new UnauthorizedAccessException("Invalid or expired token.");

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email);
        if (user == null) throw new UnauthorizedAccessException("User not found.");

        user.PasswordHash = _hasher.HashPassword(newPassword);
        await _db.SaveChangesAsync();
        await _otpService.DeleteOtpAsync($"reset:{email}");
    }

    public async Task<bool> ValidateResetTokenAsync(string email, string token)
    {
        var stored = await _otpService.GetOtpAsync($"reset:{email}");
        return !string.IsNullOrWhiteSpace(stored) && stored == token;
    }
}
