namespace SafeDeal.Application.Common.Caching;

/// <summary>
/// Cache applicatif typé, adossé à Redis.
///
/// Stratégie retenue (voir <see cref="CacheScopes"/> pour les clés) :
///
/// <list type="bullet">
/// <item>Ne sont mises en cache que des <b>lectures d'agrégats</b> : tableaux de
/// bord et compteurs d'administration. Aucune décision métier — paiement,
/// séquestre, permission, litige — ne lit le cache.</item>
/// <item><b>Expiration</b> : chaque entrée porte une durée de vie courte, qui
/// borne la fraîcheur même si une invalidation est manquée.</item>
/// <item><b>Invalidation</b> : par portée, via un compteur de génération. Une
/// écriture incrémente le compteur de la portée concernée ; toutes ses entrées
/// deviennent inatteignables d'un coup, sans balayage de l'espace de clés, et
/// les anciennes disparaissent à leur échéance.</item>
/// <item><b>Cohérence</b> : le cache est un accélérateur, jamais une source de
/// vérité. Une panne de Redis doit dégrader la latence, pas la réponse — d'où
/// les échecs silencieux côté implémentation.</item>
/// </list>
/// </summary>
public interface ICacheService
{
    Task<T?> GetAsync<T>(string key, CancellationToken ct = default);

    Task SetAsync<T>(string key, T value, TimeSpan expiry, CancellationToken ct = default);

    Task RemoveAsync(string key, CancellationToken ct = default);

    /// <summary>
    /// Génération courante d'une portée. Elle entre dans la clé effective, ce
    /// qui rend l'invalidation instantanée et sans énumération.
    /// </summary>
    Task<long> GenerationAsync(string scope, CancellationToken ct = default);

    /// <summary>Périme d'un coup toutes les entrées d'une portée.</summary>
    Task InvalidateAsync(string scope, CancellationToken ct = default);
}
