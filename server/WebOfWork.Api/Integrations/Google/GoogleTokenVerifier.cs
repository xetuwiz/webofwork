using Google.Apis.Auth;
using Microsoft.Extensions.Options;
using WebOfWork.Api.Configuration;
namespace WebOfWork.Api.Integrations.Google;
public interface IGoogleTokenVerifier { Task<GoogleJsonWebSignature.Payload> VerifyAsync(string idToken); }
public class GoogleTokenVerifier : IGoogleTokenVerifier {
    private readonly GoogleOptions _opts;
    public GoogleTokenVerifier(IOptions<GoogleOptions> opts) => _opts = opts.Value;
    public async Task<GoogleJsonWebSignature.Payload> VerifyAsync(string idToken) {
        return await GoogleJsonWebSignature.ValidateAsync(idToken, new GoogleJsonWebSignature.ValidationSettings {
            Audience = new[] { _opts.ClientId }
        });
    }
}
