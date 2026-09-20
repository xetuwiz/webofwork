using System.Security.Cryptography;

namespace WebOfWork.Api.Services;

public interface IRefreshTokenService
{
    string GenerateRefreshToken();
}

public class RefreshTokenService : IRefreshTokenService
{
    public string GenerateRefreshToken()
    {
        var randomBytes = new byte[64];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(randomBytes);
        }
        return Convert.ToBase64String(randomBytes);
    }
}
