namespace SafeDeal.Application.Common.Caching;

/// <summary>
/// Familles de durée de vie. La requête déclare à quelle famille elle
/// appartient ; la valeur en secondes reste dans la configuration, pour être
/// ajustée en exploitation sans toucher au code.
/// </summary>
public enum CacheProfile
{
    /// <summary>Tableau de bord d'un utilisateur.</summary>
    Dashboard,

    /// <summary>Compteurs de plateforme des écrans d'administration.</summary>
    AdminStats
}

/// <summary>
/// Marque une requête dont la réponse peut être servie depuis le cache.
/// Seules des lectures d'agrégats portent ce contrat : une requête qui décide
/// d'un droit, d'un montant dû ou d'un état de paiement ne doit jamais
/// l'implémenter.
/// </summary>
public interface ICachedQuery
{
    /// <summary>Portée d'invalidation à laquelle l'entrée appartient.</summary>
    string CacheScope { get; }

    /// <summary>Clé, unique dans la portée, et stable d'un appel à l'autre.</summary>
    string CacheKey { get; }

    /// <summary>
    /// Famille de durée de vie. L'expiration borne la fraîcheur même si une
    /// invalidation est perdue : c'est le filet, pas le mécanisme principal.
    /// </summary>
    CacheProfile Profile { get; }
}
