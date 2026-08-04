using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using SafeDeal.Domain.Entities;
using SafeDeal.Domain.Interfaces.Services;
using SafeDeal.Infrastructure.Services.Cache;
namespace SafeDeal.Infrastructure.Services.Auth;

public class JwtService : ITokenService
{
    private readonly IConfiguration _config;
    private readonly IRedisCacheService _cache;

    public JwtService(IConfiguration config, IRedisCacheService cache)
    {
        _config = config;
        _cache = cache;
    }

    public string GenerateAccessToken(User user)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Secret"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Role, user.Role.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"],
            audience: _config["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddDays(1),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public async Task BlacklistTokenAsync(string token, CancellationToken ct = default)
        => await _cache.SetAsync($"blacklist:{token}", "1", TimeSpan.FromDays(1), ct);

    public async Task<bool> IsTokenBlacklistedAsync(string token, CancellationToken ct = default)
        => await _cache.ExistsAsync($"blacklist:{token}", ct);
}