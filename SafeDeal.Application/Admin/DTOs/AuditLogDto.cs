namespace SafeDeal.Application.Admin.DTOs;

/// <summary>
/// Une ligne du journal d'audit telle que l'écran d'administration la lit.
/// Le journal ne contient par construction aucun secret (voir
/// <c>AuditRedaction</c>) : il est donc rendu tel quel, adresse IP comprise —
/// sans elle, une trace ne permet pas d'enquêter.
/// </summary>
public record AuditLogDto(
    int Id,
    string Action,
    int? UserId,
    string? Subject,
    string? EntityType,
    int? EntityId,
    bool Succeeded,
    string? FailureReason,
    string? IpAddress,
    string? UserAgent,
    string? Metadata,
    string CreatedAt);
