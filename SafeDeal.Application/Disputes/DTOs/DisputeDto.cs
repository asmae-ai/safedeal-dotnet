namespace SafeDeal.Application.Disputes.DTOs;

public record EvidenceDto(
    int AuthorId,
    string AuthorName,
    // "buyer" ou "vendor" : déterminé par le rôle réel de l'auteur dans la transaction.
    string SubmittedBy,
    string Description,
    IEnumerable<string> Files,
    string CreatedAt);

public record DisputeDto(
    int Id,
    int TransactionId,
    string Category,
    string Description,
    string Status,
    string CreatedAt,
    string? ResolutionNote,
    UserOpenedByDto OpenedBy,
    IEnumerable<EvidenceDto> Evidences);

public record UserOpenedByDto(int Id, string Name, string Email);
