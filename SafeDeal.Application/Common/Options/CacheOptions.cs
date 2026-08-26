namespace SafeDeal.Application.Common.Options;

/// <summary>
/// Durées de vie du cache, ajustables par configuration (section <c>Cache</c>).
/// Elles sont volontairement courtes : le cache absorbe les rafales de lecture
/// d'un écran qui se rafraîchit, il ne conserve pas un état de la journée.
/// </summary>
public class CacheOptions
{
    public const string SectionName = "Cache";

    /// <summary>Tableau de bord d'un vendeur ou d'un acheteur, en secondes.</summary>
    public int DashboardSeconds { get; set; } = 60;

    /// <summary>
    /// Compteurs de l'administration, en secondes. Plus court : ces chiffres
    /// bougent au rythme de toute la plateforme et ne sont pas tous couverts
    /// par une invalidation explicite.
    /// </summary>
    public int AdminStatsSeconds { get; set; } = 30;

    /// <summary>Permet de couper entièrement le cache sans redéploiement.</summary>
    public bool Enabled { get; set; } = true;

    public TimeSpan Dashboard => TimeSpan.FromSeconds(DashboardSeconds);
    public TimeSpan AdminStats => TimeSpan.FromSeconds(AdminStatsSeconds);
}
