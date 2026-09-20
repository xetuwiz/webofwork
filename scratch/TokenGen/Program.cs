using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace TokenGen
{
    class Program
    {
        static void Main(string[] args)
        {
            var secret = "VSOWdov6GvqGJyY/qfQDEYyd3ZkezIGRpq4kiJVsGqT4JjMg+af6OK0Q1xoY0sitSBoQHZCQiPPbjT7TPMZpiw==";
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, "1"),
                new Claim(ClaimTypes.Email, "admin@webofwork.lk"),
                new Claim("role", "Admin")
            };

            var token = new JwtSecurityToken(
                issuer: "WebOfWork",
                audience: "WebOfWork",
                claims: claims,
                expires: DateTime.UtcNow.AddDays(7),
                signingCredentials: creds
            );

            var tokenHandler = new JwtSecurityTokenHandler();
            Console.WriteLine(tokenHandler.WriteToken(token));
        }
    }
}
