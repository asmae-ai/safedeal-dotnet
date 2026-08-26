using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SafeDeal.Application.Common.Caching;
using SafeDeal.Application.Common.Options;

namespace SafeDeal.Application.Common.Behaviors;

/// <summary>
/// Sert depuis Redis les requêtes qui portent <see cref="ICachedQuery"/>.
/// Le mécanisme est ici plutôt que dans chaque handler : la règle « seules les
/// lectures d'agrégats sont mises en cache » se lit alors à un seul endroit,
/// et un handler ne peut pas cacher par mégarde un calcul de solde.
/// </summary>
public class CachingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ICacheService _cache;
    private readonly CacheOptions _options;
    private readonly ILogger<CachingBehavior<TRequest, TResponse>> _logger;

    public CachingBehavior(
        ICacheService cache,
        IOptions<CacheOptions> options,
        ILogger<CachingBehavior<TRequest, TResponse>> logger)
    {
        _cache = cache;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken ct)
    {
        if (request is not ICachedQuery query || !_options.Enabled) return await next(ct);

        // La génération courante entre dans la clé : invalider une portée revient
        // à incrémenter un compteur, sans avoir à retrouver les clés concernées.
        var generation = await _cache.GenerationAsync(query.CacheScope, ct);
        var key = $"cache:{query.CacheScope}:v{generation}:{query.CacheKey}";

        var cached = await _cache.GetAsync<TResponse>(key, ct);
        if (cached is not null)
        {
            _logger.LogDebug("Cache hit for {CacheKey}.", key);
            return cached;
        }

        var response = await next(ct);

        // Une réponse nulle n'est pas mise en cache : elle signale le plus souvent
        // un état transitoire qu'il ne faut pas figer.
        if (response is not null)
            await _cache.SetAsync(key, response, DurationFor(query.Profile), ct);

        return response;
    }

    private TimeSpan DurationFor(CacheProfile profile) => profile switch
    {
        CacheProfile.AdminStats => _options.AdminStats,
        _ => _options.Dashboard
    };
}
