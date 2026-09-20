using WebOfWork.Api.Data;
using WebOfWork.Api.Models;
using WebOfWork.Api.Models.Enums;
using WebOfWork.Api.Integrations.Google;
using Microsoft.EntityFrameworkCore;
namespace WebOfWork.Api.Services;
public interface IGoogleAuthService {
    Task<User> AuthenticateGoogleUserAsync(string idToken, UserRole requestedRole);
}
public class GoogleAuthService : IGoogleAuthService {
    private readonly IGoogleTokenVerifier _verifier;
    private readonly WebOfWorkDbContext _db;
    public GoogleAuthService(IGoogleTokenVerifier verifier, WebOfWorkDbContext db) { _verifier = verifier; _db = db; }
    public async Task<User> AuthenticateGoogleUserAsync(string idToken, UserRole requestedRole) {
        var payload = await _verifier.VerifyAsync(idToken);
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == payload.Email);
        if (user == null) {
            user = new User {
                Email = payload.Email,
                FullName = payload.Name ?? "Google User",
                Role = requestedRole,
                IsGoogleAccount = true,
                GoogleSub = payload.Subject,
                IsEmailVerified = payload.EmailVerified
            };
            _db.Users.Add(user);
        } else {
            user.IsGoogleAccount = true;
            user.GoogleSub = payload.Subject;
        }
        await _db.SaveChangesAsync();
        return user;
    }
}
