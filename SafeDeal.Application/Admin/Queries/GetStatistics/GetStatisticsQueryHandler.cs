using MediatR;
using Microsoft.EntityFrameworkCore;
using SafeDeal.Application.Admin.DTOs;
using SafeDeal.Application.Common.Interfaces;
using SafeDeal.Domain.Enums;

namespace SafeDeal.Application.Admin.Queries.GetStatistics;

public class GetStatisticsQueryHandler : IRequestHandler<GetStatisticsQuery, AdminStatsDto>
{
    private readonly IApplicationDbContext _context;
    public GetStatisticsQueryHandler(IApplicationDbContext context) => _context = context;

    public async Task<AdminStatsDto> Handle(GetStatisticsQuery request, CancellationToken ct)
    {
        var totalUsers = await _context.Users.CountAsync(ct);
        var totalVendors = await _context.Users.CountAsync(u => u.Role == UserRole.Vendor, ct);
        var totalBuyers = await _context.Users.CountAsync(u => u.Role == UserRole.Buyer, ct);
        var totalTransactions = await _context.Transactions.CountAsync(ct);
        var pendingTransactions = await _context.Transactions
            .CountAsync(t => t.Status == TransactionStatus.PendingPayment, ct);
        var completedTransactions = await _context.Transactions
            .CountAsync(t => t.Status == TransactionStatus.Closed, ct);
        var totalDisputes = await _context.Disputes.CountAsync(ct);
        var openDisputes = await _context.Disputes
            .CountAsync(d => d.Status == DisputeStatus.Open, ct);
        var pendingVerifications = await _context.IdentityVerifications
            .CountAsync(v => v.Status == IdentityStatus.Pending, ct);

        return new AdminStatsDto(
            totalUsers, totalVendors, totalBuyers,
            totalTransactions, pendingTransactions, completedTransactions,
            totalDisputes, openDisputes, pendingVerifications);
    }
}