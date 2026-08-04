namespace SafeDeal.Application.Disputes.DTOs;

public record EvidenceDto(
    string SubmittedBy,
    string Description,
    IEnumerable<string> Files,
    string CreatedAt);

public record DisputeDto(
    int Id,
    string Category,
    string Description,
    string Status,
    string CreatedAt,
    UserOpenedByDto OpenedBy,
    IEnumerable<EvidenceDto> Evidences);

public record UserOpenedByDto(int Id, string Name, string Email);