using System.Globalization;

namespace SafeDeal.Application.Common.Extensions;

public static class DecimalExtensions
{
    /// <summary>
    /// Formate un décimal pour le contrat d'API, toujours avec un point décimal.
    /// La culture du serveur ne doit jamais fuiter dans le JSON : sous une culture
    /// française, "3200,00" arrive en NaN côté frontend.
    /// </summary>
    public static string ToApiString(this decimal value)
        => value.ToString("F2", CultureInfo.InvariantCulture);
}
