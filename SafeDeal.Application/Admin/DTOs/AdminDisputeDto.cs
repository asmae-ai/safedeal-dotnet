namespace SafeDeal.Application.Admin.DTOs;

/// <summary>
/// Vue admin d'un litige : le dossier complet en une ligne, avec la transaction
/// concernée et ses deux parties, pour trancher sans requête supplémentaire.
/// </summary>
public record AdminDisputeDto(
    int Id,
    int TransactionId,
    string Ref,
    string TransactionTitle,
    string Amount,
    string Currency,
    string Category,
    string Description,
    string Status,
    string OpenedAt,
    string OpenedByRole,
    string? BuyerName,
    string? BuyerEmail,
    string VendorName,
    string VendorEmail,
    int MessagesCount,
    string? ResolutionNote);
