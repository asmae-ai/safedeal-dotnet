namespace SafeDeal.Application.Dashboard.DTOs;

/// <summary>Un point d'une série temporelle (mois, jour ou heure selon la requête).</summary>
public record SeriesPointDto(string Label, string Value, int Count);

/// <summary>Une entrée du fil d'activité, reconstituée depuis le journal des transactions.</summary>
public record ActivityItemDto(
    int TransactionId,
    string Type,
    string Title,
    string Detail,
    string CreatedAt);

public record OrderToProcessDto(
    int Id,
    string Token,
    string Title,
    string Amount,
    string Currency,
    string Status,
    string? TrackingNumber,
    string? Carrier,
    string? BuyerName,
    string CreatedAt);

public record VendorDashboardDto(
    // Fonds effectivement acquis au vendeur (transactions cloturees ou tranchees en sa faveur).
    string ReleasedRevenue,
    // Part reversee au vendeur apres commission de la plateforme.
    string NetRevenue,
    string CommissionPaid,
    // Fonds encaisses mais encore bloques en sequestre.
    string InEscrow,
    string RefundedTotal,
    string Currency,
    int TotalOrders,
    int AwaitingShipment,
    int InTransit,
    int OpenDisputes,
    // Part des transactions arrivees a leur terme qui se sont bien terminees.
    double SuccessRate,
    int CompletedOrders,
    int FinishedOrders,
    IEnumerable<SeriesPointDto> SalesSeries,
    IEnumerable<OrderToProcessDto> OrdersToProcess,
    IEnumerable<ActivityItemDto> Activity);

public record BuyerDashboardDto(
    string TotalSpent,
    string InEscrow,
    string RefundedTotal,
    string Currency,
    int TotalOrders,
    int ActiveOrders,
    int CompletedOrders,
    int OpenDisputes,
    int UnreadNotifications,
    IEnumerable<SeriesPointDto> SpendingSeries,
    IEnumerable<ActivityItemDto> Activity);

public record AdminLatestTransactionDto(
    int Id,
    string Ref,
    string Title,
    string? BuyerName,
    string VendorName,
    string Amount,
    string Currency,
    string Status,
    string CreatedAt);

public record AdminNewUserDto(
    int Id,
    string Name,
    string Role,
    string IdentityStatus,
    string CreatedAt);

public record AdminDashboardDto(
    int TotalTransactions,
    int TransactionsToday,
    string TotalVolume,
    string EscrowAmount,
    string SettledVolume,
    string Commission,
    string VolumeToday,
    string AverageAmount,
    string Currency,
    double SuccessRate,
    int SuccessfulToday,
    int TotalUsers,
    int NewUsersThisMonth,
    int PendingVerifications,
    int OpenDisputes,
    IEnumerable<SeriesPointDto> VolumeSeries,
    IEnumerable<SeriesPointDto> RevenueToday,
    IEnumerable<ActivityItemDto> Activity,
    IEnumerable<AdminLatestTransactionDto> LatestTransactions,
    IEnumerable<AdminNewUserDto> NewUsers);
