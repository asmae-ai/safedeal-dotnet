namespace SafeDeal.Application.Common.Options;

/// <summary>
/// Paramètres commerciaux de la plateforme. La commission n'est pas une donnée
/// dérivable du domaine : c'est une décision métier, donc une configuration
/// explicite plutôt qu'une constante cachée dans un calcul.
/// </summary>
public class PlatformOptions
{
    public const string SectionName = "Platform";

    /// <summary>Part prélevée par SafeDeal sur chaque transaction menée à son terme.</summary>
    public decimal CommissionRate { get; set; } = 0.05m;

    /// <summary>Devise de référence pour les agrégats multi-transactions.</summary>
    public string DefaultCurrency { get; set; } = "MAD";
}
