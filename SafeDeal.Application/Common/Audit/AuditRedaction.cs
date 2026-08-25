using System.Text.Json;

namespace SafeDeal.Application.Common.Audit;

/// <summary>
/// Dernier rempart avant écriture : même si un appelant passe par erreur une
/// donnée sensible en métadonnée, elle ne doit pas atteindre la base.
/// </summary>
public static class AuditRedaction
{
    private static readonly string[] ForbiddenKeyFragments =
    [
        "password", "motdepasse", "secret", "token", "jwt", "bearer",
        "apikey", "api_key", "authorization", "otp", "code", "pin",
        "cvv", "card", "iban", "signature", "credential"
    ];

    public const string Placeholder = "[redacted]";

    public static bool IsSensitiveKey(string key)
        => ForbiddenKeyFragments.Any(f => key.Contains(f, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Sérialise les métadonnées en masquant toute clé au nom suspect.
    /// Renvoie null quand il n'y a rien à écrire.
    /// </summary>
    public static string? Serialize(IReadOnlyDictionary<string, object?>? metadata)
    {
        if (metadata is null || metadata.Count == 0) return null;

        var safe = metadata.ToDictionary(
            pair => pair.Key,
            pair => IsSensitiveKey(pair.Key) ? Placeholder : pair.Value);

        return JsonSerializer.Serialize(safe);
    }
}
