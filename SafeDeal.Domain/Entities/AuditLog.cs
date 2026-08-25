using SafeDeal.Domain.Common;
using SafeDeal.Domain.Enums;

namespace SafeDeal.Domain.Entities;

/// <summary>
/// Trace immuable d'une action sensible. Une ligne d'audit ne se modifie ni ne
/// se supprime : elle constate un fait daté.
/// </summary>
public class AuditLog : BaseEntity
{
    /// <summary>Auteur de l'action. Nul quand elle échoue avant identification.</summary>
    public int? UserId { get; private set; }

    /// <summary>
    /// Sujet de l'action quand l'auteur n'est pas encore connu (tentative de
    /// connexion sur un compte inexistant, par exemple). Jamais un secret.
    /// </summary>
    public string? Subject { get; private set; }

    public AuditAction Action { get; private set; }
    public string? EntityType { get; private set; }
    public int? EntityId { get; private set; }

    public bool Succeeded { get; private set; }
    public string? FailureReason { get; private set; }

    public string? IpAddress { get; private set; }
    public string? UserAgent { get; private set; }

    /// <summary>Contexte additionnel non sensible, sérialisé en JSON.</summary>
    public string? Metadata { get; private set; }

    private AuditLog() { }

    public static AuditLog Record(
        AuditAction action,
        int? userId = null,
        string? subject = null,
        string? entityType = null,
        int? entityId = null,
        bool succeeded = true,
        string? failureReason = null,
        string? ipAddress = null,
        string? userAgent = null,
        string? metadata = null) => new()
        {
            Action = action,
            UserId = userId,
            Subject = Truncate(subject, 255),
            EntityType = Truncate(entityType, 100),
            EntityId = entityId,
            Succeeded = succeeded,
            FailureReason = Truncate(failureReason, 500),
            IpAddress = Truncate(ipAddress, 64),
            UserAgent = Truncate(userAgent, 512),
            Metadata = Truncate(metadata, 2000)
        };

    private static string? Truncate(string? value, int max)
        => string.IsNullOrWhiteSpace(value) ? null
            : value.Length <= max ? value : value[..max];
}
