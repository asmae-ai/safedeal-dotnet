namespace SafeDeal.Application.Admin.DTOs;

public record AdminStatsDto(
    int TotalUsers,
    int TotalVendors,
    int TotalBuyers,
    int TotalTransactions,
    int PendingTransactions,
    int CompletedTransactions,
    int TotalDisputes,
    int OpenDisputes,
    int PendingVerifications);