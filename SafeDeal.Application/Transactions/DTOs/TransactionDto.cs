namespace SafeDeal.Application.Transactions.DTOs;

public record UserSummaryDto(int Id, string Name, string Email);

public record TransactionDto(
    int Id,
    string Token,
    string Title,
    string Amount,
    string Currency,
    string Status,
    string? TrackingNumber,
    string? Carrier,
    UserSummaryDto Vendor,
    UserSummaryDto? Buyer,
    string CreatedAt,
    string UpdatedAt);