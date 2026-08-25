namespace SafeDeal.Application.Admin.DTOs;

/// <summary>
/// Compteurs d'une file de traitement admin (vérifications ou litiges).
/// Les écrans affichaient jusqu'ici des constantes parce que seule la file
/// « en attente » était exposée : sans l'historique, aucun total n'était calculable.
/// </summary>
public record AdminQueueStatsDto(
    int Total,
    int Pending,
    int Approved,
    int Rejected,
    int NewThisMonth,
    double CompletionRate);
