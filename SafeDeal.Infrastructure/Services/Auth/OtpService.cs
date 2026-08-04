using SafeDeal.Domain.Interfaces.Services;
using SafeDeal.Infrastructure.Services.Cache;
namespace SafeDeal.Infrastructure.Services.Auth;

public class OtpService : IOtpService
{
    private readonly IRedisCacheService _cache;
    private const int OtpLength = 6;
    private const int ExpiryMinutes = 10;
    private const int CooldownSeconds = 60;

    public OtpService(IRedisCacheService cache) => _cache = cache;

    public async Task<string> GenerateAndStoreAsync(string key, CancellationToken ct = default)
    {
        var code = Random.Shared.Next(100000, 999999).ToString();
        await _cache.SetAsync(key, code, TimeSpan.FromMinutes(ExpiryMinutes), ct);
        await _cache.SetAsync($"{key}_cooldown", "1", TimeSpan.FromSeconds(CooldownSeconds), ct);
        return code;
    }

    public async Task<bool> ValidateAsync(string key, string code, CancellationToken ct = default)
    {
        var stored = await _cache.GetAsync(key, ct);
        return stored == code;
    }

    public async Task InvalidateAsync(string key, CancellationToken ct = default)
        => await _cache.DeleteAsync(key, ct);

    public async Task<bool> IsOnCooldownAsync(string key, CancellationToken ct = default)
        => await _cache.ExistsAsync($"{key}_cooldown", ct);
}