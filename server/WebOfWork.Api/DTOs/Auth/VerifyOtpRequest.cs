namespace WebOfWork.Api.DTOs.Auth;
public record VerifyOtpRequest(string Email, string Otp, string Password, string FullName, int Role);
