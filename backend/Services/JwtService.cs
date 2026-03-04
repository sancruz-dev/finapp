using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using FinApp.Api.Models;
using Microsoft.IdentityModel.Tokens;

namespace FinApp.Api.Services;

public class JwtService
{
    private readonly string _secret;
    private readonly TimeSpan _expires;

    public JwtService()
    {
        _secret = Environment.GetEnvironmentVariable("JWT_SECRET") ?? "changeme_super_secret_key_here";
        var raw = Environment.GetEnvironmentVariable("JWT_EXPIRES_IN") ?? "7d";
        _expires = raw.EndsWith('d')
            ? TimeSpan.FromDays(int.Parse(raw[..^1]))
            : TimeSpan.FromHours(int.Parse(raw[..^1]));
    }

    public string Generate(User user)
    {
        var key   = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim("id",    user.Id.ToString()),
            new Claim("name",  user.Name),
            new Claim("email", user.Email),
        };

        var token = new JwtSecurityToken(
            claims:            claims,
            expires:           DateTime.UtcNow.Add(_expires),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
