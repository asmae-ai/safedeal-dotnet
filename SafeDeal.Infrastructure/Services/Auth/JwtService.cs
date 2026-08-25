using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using SafeDeal.Domain.Entities;
using SafeDeal.Domain.Interfaces.Services;
using SafeDeal.Infrastructure.Services.Cache;

namespace SafeDeal.Infrastructure.Services.Auth;

public class JwtService : ITokenService
{
    private static readonly TimeSpan AccessTokenLifetime = TimeSpan.FromHours(2);
    private static readonly TimeSpan RefreshTokenLifetime = TimeSpan.FromDays(14);

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
            expires: DateTime.UtcNow.Add(AccessTokenLifetime),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public async Task<string> IssueRefreshTokenAsync(int userId, CancellationToken ct = default)
    {
        var refreshToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();

        // Le jeton est la cle : sa valeur ne quitte jamais Redis autrement que
        // dans la reponse au client, et l'entree porte le compte associe.
        await _cache.SetAsync($"refresh:{refreshToken}", userId.ToString(), RefreshTokenLifetime, ct);
        await _cache.SetAsync($"refresh_owner:{userId}:{refreshToken}", "1", RefreshTokenLifetime, ct);

        return refreshToken;
    }

    public async Task<int?> ConsumeRefreshTokenAsync(string refreshToken, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken)) return null;

        var stored = await _cache.GetAsync($"refresh:{refreshToken}", ct);
        if (stored is null || !int.TryParse(stored, out var userId)) return null;

        // Usage unique : un jeton rejoue est un signal de vol, pas un cas normal.
        await _cache.DeleteAsync($"refresh:{refreshToken}", ct);
        await _cache.DeleteAsync($"refresh_owner:{userId}:{refreshToken}", ct);

        return userId;
    }

    public async Task RevokeRefreshTokensAsync(int userId, CancellationToken ct = default)
        => await _cache.DeleteAsync($"refresh_owner:{userId}", ct);

    public async Task BlacklistTokenAsync(string token, CancellationToken ct = default)
        => await _cache.SetAsync($"blacklist:{token}", "1", AccessTokenLifetime, ct);

    public async Task<bool> IsTokenBlacklistedAsync(string token, CancellationToken ct = default)
        => await _cache.ExistsAsync($"blacklist:{token}", ct);
}
