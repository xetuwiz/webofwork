using System;
using System.Threading;
using System.Threading.Tasks;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using WebOfWork.Api.Data;
using WebOfWork.Api.Integrations.Sms;
using WebOfWork.Api.Integrations.Sms.Strategies;
using WebOfWork.Api.Jobs;

namespace WebOfWork.Api.Services;

public interface IPhoneVerificationService
{
    Task SendPhoneOtpAsync(Guid userId, string phone);
    Task VerifyPhoneOtpAsync(Guid userId, string phone, string otp);
}

public class PhoneVerificationService : IPhoneVerificationService
{
    private readonly ISmsSender _smsSender;
    private readonly IOtpService _otpService;
    private readonly WebOfWorkDbContext _db;
    private readonly SmsStrategyFactory _smsStrategyFactory;
    private readonly IBackgroundJobClient? _jobs;

    public PhoneVerificationService(ISmsSender smsSender, IOtpService otpService, WebOfWorkDbContext db, SmsStrategyFactory factory, IBackgroundJobClient? jobs = null)
    {
        _smsSender = smsSender;
        _otpService = otpService;
        _db = db;
        _smsStrategyFactory = factory;
        _jobs = jobs;
    }

    public async Task SendPhoneOtpAsync(Guid userId, string phone)
    {
        var normalizedPhone = AndroidSmsGatewaySmsSender.NormalizeSriLankanPhoneNumber(phone);
        var code = new Random().Next(100000, 999999).ToString();
        await _otpService.SetOtpAsync($"phone:{userId}", code, TimeSpan.FromMinutes(10));

        if (_jobs is not null)
        {
            _jobs.Enqueue<SendPhoneOtpJob>(job => job.ExecuteAsync(normalizedPhone, code, CancellationToken.None));
            return;
        }

        var strategy = _smsStrategyFactory.GetStrategy("otp");
        var message = strategy.GenerateMessage(code);
        await _smsSender.SendSmsAsync(normalizedPhone, message);
    }

    public async Task VerifyPhoneOtpAsync(Guid userId, string phone, string otp)
    {
        var normalizedPhone = AndroidSmsGatewaySmsSender.NormalizeSriLankanPhoneNumber(phone);
        var stored = await _otpService.GetOtpAsync($"phone:{userId}");
        if (stored != otp) throw new UnauthorizedAccessException("Invalid OTP");

        var profile = await _db.FreelancerProfiles.FirstOrDefaultAsync(p => p.UserId == userId);
        if (profile != null)
        {
            profile.Phone = normalizedPhone;
            profile.IsPhoneVerified = true;
            await _db.SaveChangesAsync();
        }
        await _otpService.DeleteOtpAsync($"phone:{userId}");
    }
}
