namespace SafeDeal.Application.Common.Caching;

/// <summary>
/// Portées d'invalidation et clés du cache, réunies ici pour qu'une écriture et
/// la lecture qu'elle périme ne puissent pas diverger.
///
/// Deux portées suffisent aujourd'hui :
/// <list type="bullet">
/// <item><c>dash:u:{userId}</c> — ce qu'un utilisateur voit de ses propres
/// transactions. Une transaction ne concerne que ses deux parties : la périmer
/// pour tout le monde gaspillerait le cache.</item>
/// <item><c>dash:admin</c> — les compteurs de plateforme, communs à tous les
/// administrateurs.</item>
/// </list>
/// </summary>
public static class CacheScopes
{
    /// <summary>Tableau de bord d'un utilisateur donné.</summary>
    public static string User(int userId) => $"dash:u:{userId}";

    /// <summary>Tableau de bord et files d'attente de l'administration.</summary>
    public const string Admin = "dash:admin";
}

/// <summary>Clés stables à l'intérieur d'une portée.</summary>
public static class CacheKeys
{
    public const string VendorDashboard = "vendor";
    public const string BuyerDashboard = "buyer";
    public static string AdminDashboard(string range) => $"admin:{range}";
    public const string AdminStats = "stats";
    public const string IdentityStats = "stats:identity";
    public const string DisputeStats = "stats:dispute";
}
