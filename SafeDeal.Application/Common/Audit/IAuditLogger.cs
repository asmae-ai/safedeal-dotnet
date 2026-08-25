using SafeDeal.Domain.Enums;

namespace SafeDeal.Application.Common.Audit;

/// <param name="Subject">
/// Identifiant lisible du sujet quand l'auteur n'est pas connu (e-mail d'une
/// tentative de connexion). Ne doit jamais porter de secret.
/// </param>
public record AuditEntry(
    AuditAction Action,
    int? UserId = null,
    string? Subject = null,
    string? EntityType = null,
    int? EntityId = null,
    bool Succeeded = true,
    string? FailureReason = null,
    IReadOnlyDictionary<string, object?>? Metadata = null);

public interface IAuditLogger
{
    /// <summary>
    /// Enregistre une action sensible. L'écriture ne doit jamais faire échouer
    /// l'opération métier appelante : un audit indisponible se journalise, il
    /// n'annule pas un paiement.
    /// </summary>
    Task LogAsync(AuditEntry entry, CancellationToken ct = default);
}
