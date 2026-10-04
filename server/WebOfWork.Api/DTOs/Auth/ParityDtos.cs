namespace WebOfWork.Api.DTOs.Auth;

public sealed record ParityRegisterOtpRequest(string FullName, string Email, string Password);
public sealed record ParityVerifyOtpRequest(string Email, string OtpCode);
public sealed record ParityResendOtpRequest(string Email);
public sealed record ParityGoogleLoginRequest(string Token);
public sealed record ParityChangePasswordRequest(string CurrentPassword, string NewPassword, string? ConfirmPassword = null);
public sealed record ParityDeleteAccountRequest(string? Password = null, string? Otp = null);
public sealed record ParityResetPasswordRequest(string Uid, string Token, string NewPassword, string? ConfirmPassword = null);
public sealed record PendingRegistration(string FullName, string Email, string Password);
public sealed record ResetGrant(string UserId, string Email);
