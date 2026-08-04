using StackExchange.Redis;

namespace SafeDeal.Infrastructure.Services.Cache;

public interface IRedisCacheService
{
    Task SetAsync(string key, string value, TimeSpan expiry, CancellationToken ct = default);
    Task<string?> GetAsync(string key, CancellationToken ct = default);
    Task DeleteAsync(string key, CancellationToken ct = default);
    Task<bool> ExistsAsync(string key, CancellationToken ct = default);
}

public class RedisCacheService : IRedisCacheService
{
    private readonly IDatabase _db;

    public RedisCacheService(IConnectionMultiplexer redis)
        => _db = redis.GetDatabase();

    public async Task SetAsync(string key, string value, TimeSpan expiry, CancellationToken ct = default)
        => await _db.StringSetAsync(key, value, expiry);

    public async Task<string?> GetAsync(string key, CancellationToken ct = default)
        => await _db.StringGetAsync(key);

    public async Task DeleteAsync(string key, CancellationToken ct = default)
        => await _db.KeyDeleteAsync(key);

    public async Task<bool> ExistsAsync(string key, CancellationToken ct = default)
        => await _db.KeyExistsAsync(key);
}