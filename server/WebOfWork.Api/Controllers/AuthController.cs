using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using WebOfWork.Api.DTOs.Auth;
using WebOfWork.Api.Models;
using WebOfWork.Api.Models.Enums;
using WebOfWork.Api.Services;

namespace WebOfWork.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _auth;
    private readonly IGoogleAuthService _google;
    private readonly IPasswordResetService _reset;
    private readonly IJwtTokenService _jwt;

    public AuthController(IAuthService auth, IGoogleAuthService google, IPasswordResetService reset, IJwtTokenService jwt)
    {
        _auth = auth; _google = google; _reset = reset; _jwt = jwt;
    }

    private Guid UserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private static string Role(User u) => u.CurrentRole.ToString().ToLowerInvariant();
    private static bool Active(User u) => u.Status == UserStatus.Active;

    [HttpPost("send-otp")]
    [AllowAnonymous]
    public async Task<IActionResult> SendOtp([FromBody] ParityRegisterOtpRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.FullName) || request.FullName.Length > 40) return BadRequest(new { full_name = new[] { "This field is required." } });
        if (string.IsNullOrWhiteSpace(request.Email) || !request.Email.Contains('@')) return BadRequest(new { email = new[] { "Enter a valid email address." } });
        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 6) return BadRequest(new { password = new[] { "This field must be at least 6 characters." } });
        // The reference rejects duplicate emails at send-otp time (RegisterSerializer.validate_email),
        // not at verify time. Without this the user only finds out via a generic 500.
        if (await _auth.EmailExistsAsync(request.Email.Trim().ToLowerInvariant())) return BadRequest(new { email = new[] { "Email already exists" } });
        try
        {
            await _auth.CacheRegistrationAsync(request.Email, new PendingRegistration(request.FullName, request.Email, request.Password), TimeSpan.FromMinutes(10));
            await _auth.SendAccountOtpAsync(request.Email, "email_verify", TimeSpan.FromMinutes(5));
            return Ok(new { message = "OTP sent to your email. Please verify." });
        }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPost("verify-otp")]
    [AllowAnonymous]
    public async Task<IActionResult> VerifyOtp([FromBody] ParityVerifyOtpRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.OtpCode)) return BadRequest(new { error = "Email and OTP are required." });
        // Clients paste codes straight out of the email body, which carries leading and
        // trailing whitespace, and SMS apps often render them as "760 666". Strip every
        // non-digit so a paste cannot cause a false "Invalid OTP".
        var otp = new string((request.OtpCode ?? string.Empty).Where(char.IsDigit).ToArray());
        if (!await _auth.VerifyAccountOtpAsync(request.Email.Trim(), "email_verify", otp)) return BadRequest(new { error = "Invalid OTP" });
        var pending = await _auth.GetRegistrationAsync(request.Email);
        if (pending == null) return BadRequest(new { error = "Registration session expired. Please register again." });
        try
        {
            var user = await _auth.CreateRegistrationUserAsync(pending);
            await _auth.DeleteRegistrationAsync(request.Email);
            var refresh = await _auth.IssueRefreshTokenAsync(user);
            SetRefreshCookie(refresh);
            // Emit both keys: OtpModal.jsx reads res.access, while older the reference clients
            // read access_token. Returning only one silently left the user logged out.
            return Ok(new { message = "Email verified successfully.", user = new { id = user.Id, email = user.Email, full_name = user.FullName, current_role = Role(user) }, access = _jwt.GenerateAccessToken(user), access_token = _jwt.GenerateAccessToken(user), refresh });
        }
        catch (Exception) { return StatusCode(500, new { error = "An unexpected error occurred during verification" }); }
    }

    [HttpPost("resend-otp")]
    [AllowAnonymous]
    public async Task<IActionResult> ResendOtp([FromBody] ParityResendOtpRequest request)
    {
        if (await _auth.GetRegistrationAsync(request.Email.Trim()) == null) return BadRequest(new { non_field_errors = new[] { "Registration expired. Please register again." } });
        try { await _auth.SendAccountOtpAsync(request.Email.Trim(), "email_verify", TimeSpan.FromMinutes(5)); return Ok(new { message = "OTP resent successfully." }); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password)) return BadRequest(new { error = "Email and password are required" });
        User user;
        try { user = await _auth.LoginAsync(request.Email.Trim(), request.Password); }
        catch (UnauthorizedAccessException) { return Unauthorized(new { error = "Invalid credentials" }); }
        if (!Active(user)) return StatusCode(403, new { error = "Your account has been blocked by admin" });
        var refresh = await _auth.IssueRefreshTokenAsync(user);
        SetRefreshCookie(refresh);
        // Return the refresh token in the body as well as the cookie. Browsers use
        // the cookie; native clients have no cookie jar, so body-only left them
        // unable to ever renew and every session died at access-token expiry.
        return Ok(new { access = _jwt.GenerateAccessToken(user), refresh, user = new { id = user.Id, email = user.Email, full_name = user.FullName, is_admin = user.Role == UserRole.Admin, current_role = Role(user), is_freelancer = user.IsFreelancer } });
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<IActionResult> Refresh([FromBody] RefreshTokenRequest? request = null)
    {
        // Cookie first (web), body second (native) -- see RefreshTokenRequest.
        var token = Request.Cookies["refresh_token"];
        if (string.IsNullOrWhiteSpace(token)) token = request?.Refresh;
        if (string.IsNullOrWhiteSpace(token)) return Unauthorized(new { error = "No refresh token" });
        var user = await _auth.ValidateRefreshTokenAsync(token);
        return user == null ? Unauthorized(new { error = "Invalid refresh token" }) : Ok(new { access = _jwt.GenerateAccessToken(user) });
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> Me()
    {
        var user = await _auth.GetUserByIdAsync(UserId());
        return user == null ? NotFound() : Ok(new { user = new { id = user.Id, full_name = user.FullName, email = user.Email, profile_photo = user.ProfilePhoto, is_freelancer = user.IsFreelancer, current_role = Role(user), is_google_account = user.IsGoogleAccount, is_admin = user.Role == UserRole.Admin, is_active = Active(user), created_at = user.CreatedAt } });
    }

    [HttpPost("logout")]
    [AllowAnonymous]
    public async Task<IActionResult> Logout([FromBody] RefreshTokenRequest? request = null, CancellationToken ct = default)
    {
        // Cookie first (web), body second (native) -- the same contract as
        // `Refresh`, and for the same reason: the web client authenticates with
        // the HttpOnly cookie, the Flutter app has no cookie jar and sends the
        // token in the body.
        var token = Request.Cookies["refresh_token"];
        if (string.IsNullOrWhiteSpace(token)) token = request?.Refresh;

        // Server-side revocation, not just deleting the cookie.
        //
        // Deleting the cookie only removes the browser's copy. The token itself
        // stayed valid in the database for its full 7-day expiry, and it is also
        // returned in the login and refresh response *bodies*, so a native client
        // never sent it as a cookie at all -- its logout removed nothing.
        await _auth.RevokeRefreshTokenAsync(token, ct);

        Response.Cookies.Delete("refresh_token", new CookieOptions { Path = "/" });
        return Ok(new { message = "Logged out successfully" });
    }

    [HttpPost("forgot-password")]
    [AllowAnonymous]
    public async Task<IActionResult> Forgot([FromBody] ForgotPasswordRequest request)
    {
        await _reset.CreateResetGrantAsync(request.Email);
        return Ok(new { message = "Password reset link sent if the email exists." });
    }

    [HttpGet("reset-password/validate")]
    [AllowAnonymous]
    public async Task<IActionResult> ValidateReset([FromQuery] string uid, [FromQuery] string token)
    {
        var valid = await _reset.ValidateResetTokenAsync(uid, token) != null;
        return valid ? Ok(new { valid = true }) : BadRequest(new { valid = false, error = "Reset link has expired or is invalid" });
    }

    [HttpPost("reset-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ResetPassword([FromBody] ParityResetPasswordRequest request)
    {
        if (request.NewPassword != request.ConfirmPassword) return BadRequest(new { non_field_errors = new[] { "Passwords do not match" } });
        try { await _reset.ResetPasswordAsync(request.Uid, request.Token, request.NewPassword); return Ok(new { message = "Password reset successful" }); }
        catch (UnauthorizedAccessException ex) { return BadRequest(new { non_field_errors = new[] { ex.Message } }); }
    }

    [HttpPost("google")]
    [AllowAnonymous]
    public async Task<IActionResult> Google([FromBody] ParityGoogleLoginRequest request)
    {
        try
        {
            var user = await _google.AuthenticateGoogleUserAsync(request.Token, UserRole.Client);
            if (user.IsDeleted) return Unauthorized(new { error = "Invalid credentials" });
            var refresh = await _auth.IssueRefreshTokenAsync(user); SetRefreshCookie(refresh);
            return Ok(new { access = _jwt.GenerateAccessToken(user), refresh, user = new { id = user.Id, email = user.Email, full_name = user.FullName, profile_photo = user.ProfilePhoto, is_admin = user.Role == UserRole.Admin, current_role = Role(user), is_freelancer = user.IsFreelancer } });
        }
        catch { return BadRequest(new { error = "Invalid Google token" }); }
    }

    [HttpPost("change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword([FromBody] ParityChangePasswordRequest request)
    {
        if (request.NewPassword != request.ConfirmPassword) return BadRequest(new { non_field_errors = new[] { "Passwords do not match." } });
        try { await _auth.ChangePasswordAsync(UserId(), request.CurrentPassword, request.NewPassword); return Ok(new { detail = "Password updated successfully." }); }
        catch (UnauthorizedAccessException ex) { return BadRequest(new { current_password = new[] { ex.Message } }); }
    }

    [HttpPost("request-delete-otp")]
    [Authorize]
    public async Task<IActionResult> RequestDeleteOtp()
    {
        var user = await _auth.GetUserByIdAsync(UserId());
        if (user == null || !user.IsGoogleAccount) return BadRequest(new { error = "OTP request is only available for Google accounts. Please use your password." });
        try { await _auth.SendAccountOtpAsync(user.Email, "account_deletion", TimeSpan.FromMinutes(5)); return Ok(new { message = "Verification code sent to your email." }); }
        catch (InvalidOperationException ex) { return StatusCode(429, new { error = ex.Message }); }
    }

    [HttpPost("delete-account")]
    [Authorize]
    public async Task<IActionResult> DeleteAccount([FromBody] ParityDeleteAccountRequest request)
    {
        try { await _auth.DeleteUserAccountAsync(UserId(), request.Password, request.Otp); Response.Cookies.Delete("refresh_token", new CookieOptions { Path = "/" }); return Ok(new { message = "Account deleted successfully." }); }
        catch (Exception ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPost("verify-email")]
    [Authorize]
    public async Task<IActionResult> VerifyEmail([FromBody] VerifyEmailChangeRequest request)
    {
        var valid = await _auth.VerifyEmailChangeAsync(UserId(), request.Otp);
        return valid ? Ok(new { verified = true }) : BadRequest(new { verified = false, error = "Invalid or expired OTP." });
    }

    [HttpPut("me")]
    [Authorize]
    public async Task<IActionResult> UpdateMe([FromBody] UpdateMeRequest request)
    {
        var user = await _auth.UpdateMeAsync(UserId(), request.FullName, request.Email);
        return Ok(new { user = new { id = user.Id, full_name = user.FullName, email = user.Email, profile_photo = user.ProfilePhoto, is_freelancer = user.IsFreelancer, current_role = Role(user), is_google_account = user.IsGoogleAccount, is_admin = user.Role == UserRole.Admin, is_active = Active(user), created_at = user.CreatedAt } });
    }

    // Secure must follow the REQUEST scheme, not the environment. The dev API is
    // served over HTTPS (https://localhost:5001), and a browser refuses to store a
    // non-Secure cookie received over HTTPS -- so keying this off IsDevelopment()
    // silently broke refresh in local dev (401 "No refresh token" forever).
    // Plain http (e.g. an http-only local run) still gets a non-Secure cookie.
    //
    // SameSite must follow the same split, and this was the bug that logged every
    // user out on every page refresh.
    //
    // The Vite dev server is plain HTTP (http://localhost:5173) while the API is
    // HTTPS (https://localhost:5001). SameSite is *schemeful*: an http page and an
    // https page on the same host are CROSS-SITE. A Lax cookie is therefore
    // withheld from the SPA's XHR to the API -- it rides along on top-level
    // navigations only. So `POST /auth/refresh` arrived with no cookie at all,
    // returned 401 "No refresh token", and the client's reaction to a 401 was
    // `dispatch(logout())`, which also wipes redux-persist. A still-valid 7-day
    // session was destroyed on every reload.
    //
    // `SameSite=None` is the only value that works across a scheme boundary, and
    // browsers only accept it alongside `Secure`, which `isHttps` already
    // guarantees here. Over plain http we keep Lax, because None without Secure is
    // rejected outright.
    //
    // What this gives up: a cross-site page could now trigger a refresh *request*
    // carrying the cookie. It cannot use the result -- the response body is an
    // access token, unreadable cross-origin because CORS grants only configured
    // origins, and refresh does not rotate or invalidate the stored token. Lax
    // remains in force for the plain-http listener.
    private void SetRefreshCookie(string token)
    {
        var isHttps = Request.IsHttps
            || string.Equals(Request.Headers["X-Forwarded-Proto"], "https", StringComparison.OrdinalIgnoreCase);
        Response.Cookies.Append("refresh_token", token, new CookieOptions
        {
            HttpOnly = true,
            Secure = isHttps,
            SameSite = isHttps ? SameSiteMode.None : SameSiteMode.Lax,
            MaxAge = TimeSpan.FromSeconds(604800),
            Path = "/",
        });
    }
}
