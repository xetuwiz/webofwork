namespace WebOfWork.Api.DTOs.Profiles;

public record PhoneOtpRequest(string Phone);
public record VerifyPhoneOtpRequest(string Phone, string Otp);
