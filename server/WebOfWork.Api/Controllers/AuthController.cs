using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using WebOfWork.Api.DTOs.Auth;
using WebOfWork.Api.Services;

namespace WebOfWork.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly IGoogleAuthService _googleAuthService;
    private readonly IPasswordResetService _passwordResetService;
    private readonly IJwtTokenService _jwtService;
    private readonly IRefreshTokenService _refreshService;

    public AuthController(
        IAuthService authService,
        IGoogleAuthService googleAuthService,
        IPasswordResetService passwordResetService,
        IJwtTokenService jwtService,
        IRefreshTokenService refreshService)
    {
        _authService = authService;
        _googleAuthService = googleAuthService;
        _passwordResetService = passwordResetService;
        _jwtService = jwtService;
        _refreshService = refreshService;
    }

    private Guid GetUserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpPost("send-otp")]
    [AllowAnonymous]
    public async Task<IActionResult> SendOtp([FromBody] SendOtpRequest request, [FromServices] IValidator<SendOtpRequest> validator)
    {
        var validation = await validator.ValidateAsync(request);
        if (!validation.IsValid) return BadRequest(validation.Errors);

        try {
            await _authService.SendOtpAsync(request.Email);
            return Ok(new { message = "OTP sent." });
        } catch (InvalidOperationException ex) {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("verify-otp")]
    [AllowAnonymous]
    public async Task<IActionResult> VerifyOtp([FromBody] VerifyOtpRequest request, [FromServices] IValidator<VerifyOtpRequest> validator)
    {
        var validation = await validator.ValidateAsync(request);
        if (!validation.IsValid) return BadRequest(validation.Errors);

        try {
            var user = await _authService.VerifyOtpAndCreateUserAsync(request.Email, request.Otp, request.Password, request.FullName, (Models.Enums.UserRole)request.Role);
            var accessToken = _jwtService.GenerateAccessToken(user);
            var refreshToken = _refreshService.GenerateRefreshToken();
            await _authService.UpdateRefreshTokenAsync(user.Id, refreshToken, DateTime.UtcNow.AddDays(7));
            
            Response.Cookies.Append("refreshToken", refreshToken, new CookieOptions { HttpOnly = true, Secure = true, SameSite = SameSiteMode.Strict });
            return Ok(new LoginResponse(accessToken, user.Id, user.Email, user.FullName, user.Role));
        } catch (Exception ex) {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, [FromServices] IValidator<LoginRequest> validator)
    {
        var validation = await validator.ValidateAsync(request);
        if (!validation.IsValid) return BadRequest(validation.Errors);

        try {
            var user = await _authService.LoginAsync(request.Email, request.Password);
            var accessToken = _jwtService.GenerateAccessToken(user);
            var refreshToken = _refreshService.GenerateRefreshToken();
            await _authService.UpdateRefreshTokenAsync(user.Id, refreshToken, DateTime.UtcNow.AddDays(7));
            
            Response.Cookies.Append("refreshToken", refreshToken, new CookieOptions { HttpOnly = true, Secure = true, SameSite = SameSiteMode.Strict });
            return Ok(new LoginResponse(accessToken, user.Id, user.Email, user.FullName, user.Role));
        } catch (UnauthorizedAccessException) {
            return Unauthorized(new { error = "Invalid credentials or account blocked." });
        }
    }

    [HttpPost("google")]
    [AllowAnonymous]
    public async Task<IActionResult> GoogleLogin([FromBody] GoogleLoginRequest request, [FromServices] IValidator<GoogleLoginRequest> validator)
    {
        var validation = await validator.ValidateAsync(request);
        if (!validation.IsValid) return BadRequest(validation.Errors);

        try {
            var user = await _googleAuthService.AuthenticateGoogleUserAsync(request.IdToken, (Models.Enums.UserRole)request.Role);
            var accessToken = _jwtService.GenerateAccessToken(user);
            var refreshToken = _refreshService.GenerateRefreshToken();
            await _authService.UpdateRefreshTokenAsync(user.Id, refreshToken, DateTime.UtcNow.AddDays(7));
            
            Response.Cookies.Append("refreshToken", refreshToken, new CookieOptions { HttpOnly = true, Secure = true, SameSite = SameSiteMode.Strict });
            return Ok(new LoginResponse(accessToken, user.Id, user.Email, user.FullName, user.Role));
        } catch (Exception ex) {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    public IActionResult Refresh()
    {
        // Placeholder for refresh logic due to brevity
        return Ok(new { message = "Refresh endpoint hit" });
    }

    [HttpPost("logout")]
    [Authorize]
    public IActionResult Logout()
    {
        Response.Cookies.Delete("refreshToken");
        return Ok(new { message = "Logged out" });
    }

    [HttpGet("me")]
    [Authorize]
    public IActionResult GetMe()
    {
        return Ok(new {
            Id = GetUserId(),
            Email = User.FindFirstValue(ClaimTypes.Email),
            Role = User.FindFirstValue("role")
        });
    }

    [HttpPatch("me")]
    [Authorize]
    public async Task<IActionResult> UpdateMe([FromBody] UpdateMeRequest request, [FromServices] IValidator<UpdateMeRequest> validator)
    {
        var validation = await validator.ValidateAsync(request);
        if (!validation.IsValid) return BadRequest(validation.Errors);

        try {
            var user = await _authService.UpdateMeAsync(GetUserId(), request.FullName, request.Email);
            return Ok(new { user.Id, user.Email, user.FullName });
        } catch (Exception ex) {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("forgot-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request, [FromServices] IValidator<ForgotPasswordRequest> validator)
    {
        var validation = await validator.ValidateAsync(request);
        if (!validation.IsValid) return BadRequest(validation.Errors);

        await _passwordResetService.GenerateResetTokenAsync(request.Email);
        return Ok(new { message = "If the email exists, a reset token was sent." });
    }

    [HttpPost("reset-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request, [FromServices] IValidator<ResetPasswordRequest> validator)
    {
        var validation = await validator.ValidateAsync(request);
        if (!validation.IsValid) return BadRequest(validation.Errors);

        try {
            await _passwordResetService.ResetPasswordAsync(request.Email, request.Token, request.NewPassword);
            return Ok(new { message = "Password reset successful." });
        } catch (UnauthorizedAccessException ex) {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        try {
            await _authService.ChangePasswordAsync(GetUserId(), request.CurrentPassword, request.NewPassword);
            return Ok(new { message = "Password changed successfully." });
        } catch (Exception ex) {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("request-delete-otp")]
    [Authorize]
    public async Task<IActionResult> RequestDeleteOtp()
    {
        await _authService.RequestDeleteOtpAsync(GetUserId());
        return Ok(new { message = "Deletion OTP sent." });
    }

    [HttpPost("delete-account")]
    [Authorize]
    public async Task<IActionResult> DeleteAccount([FromBody] DeleteAccountRequest request)
    {
        try {
            await _authService.DeleteAccountAsync(GetUserId(), request.Otp);
            Response.Cookies.Delete("refreshToken");
            return Ok(new { message = "Account successfully deleted." });
        } catch (Exception ex) {
            return BadRequest(new { error = ex.Message });
        }
    }
}
