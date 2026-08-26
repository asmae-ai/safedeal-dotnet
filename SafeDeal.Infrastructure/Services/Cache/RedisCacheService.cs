using System.Text.Json;
using Microsoft.Extensions.Logging;
using SafeDeal.Application.Common.Caching;
using StackExchange.Redis;

namespace SafeDeal.Infrastructure.Services.Cache;

/// <summary>
/// Accès brut à Redis : codes OTP, jetons révoqués, valeurs de session.
/// Les appelants y stockent des chaînes qu'ils composent eux-mêmes.
/// </summary>
public interface IRedisCacheService
{
    Task SetAsync(string key, string value, TimeSpan expiry, CancellationToken ct = default);
    Task<string?> GetAsync(string key, CancellationToken ct = default);
    Task DeleteAsync(string key, CancellationToken ct = default);
    Task<bool> ExistsAsync(string key, CancellationToken ct = default);
}

/// <summary>
/// Une seule implémentation pour les deux contrats : l'accès brut historique et
/// le cache applicatif typé. Les deux parlent au même Redis, avec la même
/// connexion, et doivent partager la même politique de panne.
///
/// Cette politique : le cache ne fait jamais échouer une requête. Redis
/// injoignable, réponse illisible, format changé depuis le déploiement
/// précédent — chaque cas retombe sur un défaut d'échec, et l'appelant
/// recalcule. C'est le prix à payer pour qu'un incident d'infrastructure
/// dégrade la latence sans dégrader les réponses.
///
/// Cette clémence ne vaut que pour <see cref="ICacheService"/>, réservé aux
/// agrégats de lecture. L'accès brut, lui, porte des jetons révoqués et des
/// codes à usage unique : une erreur y remonte, car « je ne sais pas » ne peut
/// pas y valoir « non révoqué ».
/// </summary>
public class RedisCacheService : IRedisCacheService, ICacheService
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private readonly IDatabase _db;
    private readonly ILogger<RedisCacheService> _logger;

    public RedisCacheService(IConnectionMultiplexer redis, ILogger<RedisCacheService> logger)
    {
        _db = redis.GetDatabase();
        _logger = logger;
    }

    // ------------------------------------------------------------- accès brut

    public async Task SetAsync(string key, string value, TimeSpan expiry, CancellationToken ct = default)
        => await _db.StringSetAsync(key, value, expiry);

    public async Task<string?> GetAsync(string key, CancellationToken ct = default)
        => await _db.StringGetAsync(key);

    public async Task DeleteAsync(string key, CancellationToken ct = default)
        => await _db.KeyDeleteAsync(key);

    public async Task<bool> ExistsAsync(string key, CancellationToken ct = default)
        => await _db.KeyExistsAsync(key);

    // -------------------------------------------------------- cache applicatif

    public async Task<T?> GetAsync<T>(string key, CancellationToken ct = default)
    {
        try
        {
            var raw = await _db.StringGetAsync(key);
            if (raw.IsNullOrEmpty) return default;

            return JsonSerializer.Deserialize<T>(raw.ToString(), SerializerOptions);
        }
        catch (Exception ex)
        {
            // Inclut le cas d'une entrée écrite par une version antérieure du
            // DTO : illisible n'est pas une erreur, c'est une absence.
            _logger.LogWarning(ex, "Cache read failed for {CacheKey}; falling back to the source.", key);
            return default;
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan expiry, CancellationToken ct = default)
    {
        try
        {
            await _db.StringSetAsync(key, JsonSerializer.Serialize(value, SerializerOptions), expiry);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cache write failed for {CacheKey}; the response is unaffected.", key);
        }
    }

    public async Task RemoveAsync(string key, CancellationToken ct = default)
    {
        try
        {
            await _db.KeyDeleteAsync(key);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cache eviction failed for {CacheKey}.", key);
        }
    }

    public async Task<long> GenerationAsync(string scope, CancellationToken ct = default)
    {
        try
        {
            var raw = await _db.StringGetAsync(GenerationKey(scope));
            return (long?)raw ?? 0;
        }
        catch (Exception ex)
        {
            // Génération inconnue : on se rabat sur 0. La lecture qui suit sera
            // au pire un défaut de cache, jamais une donnée périmée servie à
            // la place d'une donnée fraîche.
            _logger.LogWarning(ex, "Cache generation read failed for scope {CacheScope}.", scope);
            return 0;
        }
    }

    public async Task InvalidateAsync(string scope, CancellationToken ct = default)
    {
        try
        {
            // INCR est atomique : deux écritures concurrentes ne peuvent pas
            // ressusciter une génération déjà dépassée.
            await _db.StringIncrementAsync(GenerationKey(scope));
        }
        catch (Exception ex)
        {
            // Une invalidation perdue est rattrapée par l'expiration de l'entrée.
            _logger.LogWarning(ex, "Cache invalidation failed for scope {CacheScope}.", scope);
        }
    }

    private static string GenerationKey(string scope) => $"cache:gen:{scope}";
}
