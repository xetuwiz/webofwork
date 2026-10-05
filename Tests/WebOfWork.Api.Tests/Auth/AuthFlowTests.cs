using Xunit;
using WebOfWork.Api.DTOs.Auth;
using WebOfWork.Api.Validators.Auth;

namespace WebOfWork.Api.Tests.Auth;

public class AuthFlowTests
{
    [Fact]
    public void SendOtpValidator_ShouldRequireValidEmail()
    {
        var validator = new SendOtpValidator();
        var result = validator.Validate(new SendOtpRequest("invalid-email"));
        Assert.False(result.IsValid);
        
        var validResult = validator.Validate(new SendOtpRequest("test@xetuwiz.de"));
        Assert.True(validResult.IsValid);
    }
    
    [Fact]
    public void VerifyOtpValidator_ShouldRequireStrongPassword()
    {
        var validator = new VerifyOtpValidator();
        var result = validator.Validate(new VerifyOtpRequest("test@xetuwiz.de", "123456", "weak", "John", 1));
        Assert.False(result.IsValid); // Password too short
    }
    
    [Fact]
    public void LoginValidator_ShouldRequireFields()
    {
        var validator = new LoginValidator();
        var result = validator.Validate(new LoginRequest("", ""));
        Assert.False(result.IsValid);
    }
}
