using System.Globalization;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SafeDeal.Application.Common.Extensions;
using SafeDeal.Application.Common.Interfaces;
using SafeDeal.Application.Common.Options;
using SafeDeal.Application.Dashboard.DTOs;
using SafeDeal.Domain.Entities;
using SafeDeal.Domain.Enums;

namespace SafeDeal.Application.Dashboard.Queries.GetAdminDashboard;

public class GetAdminDashboardQueryHandler : IRequestHandler<GetAdminDashboardQuery, AdminDashboardDto>
{
    private static readonly CultureInfo Fr = CultureInfo.GetCultureInfo("fr-FR");

    private readonly IApplicationDbContext _context;
    private readonly PlatformOptions _platform;

    public GetAdminDashboardQueryHandler(IApplicationDbContext context, IOptions<PlatformOptions> platform)
    {
        _context = context;
        _platform = platform.Value;
    }

    public async Task<AdminDashboardDto> Handle(GetAdminDashboardQuery request, CancellationToken ct)
    {
        var transactions = await _context.Transactions
            .Include(t => t.Vendor)
            .Include(t => t.Buyer)
            .ToListAsync(ct);

        var settled = transactions
            .Where(t => t.Status is TransactionStatus.Closed or TransactionStatus.Resolved)
            .ToList();

        var escrow = transactions
            .Where(t => t.Status is TransactionStatus.PaymentReceived
                                 or TransactionStatus.InShipping
                                 or TransactionStatus.Delivered
                                 or TransactionStatus.Dispute)
            .Sum(t => t.Amount.Amount);

        var settledVolume = settled.Sum(t => t.Amount.Amount);
        var totalVolume = transactions.Sum(t => t.Amount.Amount);

        // La commission n'est acquise que sur les transactions menees a leur terme.
        var commission = Math.Round(settledVolume * _platform.CommissionRate, 2);

        var finished = transactions.Count(t => t.Status is TransactionStatus.Closed
                                                        or TransactionStatus.Resolved
                                                        or TransactionStatus.Refunded
                                                        or TransactionStatus.Cancelled);
        var successRate = finished == 0 ? 0 : Math.Round(settled.Count * 100.0 / finished, 1);

        var todayStart = DateTime.UtcNow.Date;
        var todays = transactions.Where(t => t.CreatedAt >= todayStart).ToList();
        var settledToday = settled.Where(t => t.UpdatedAt >= todayStart).ToList();

        var monthStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var totalUsers = await _context.Users.CountAsync(ct);
        var newUsersThisMonth = await _context.Users.CountAsync(u => u.CreatedAt >= monthStart, ct);
        var pendingVerifications = await _context.IdentityVerifications
            .CountAsync(v => v.Status == IdentityStatus.Pending, ct);
        var openDisputes = await _context.Disputes
            .CountAsync(d => d.Status != DisputeStatus.Resolved && d.Status != DisputeStatus.Closed, ct);

        var newUsers = await _context.Users
            .OrderByDescending(u => u.CreatedAt)
            .Take(5)
            .Select(u => new AdminNewUserDto(
                u.Id, u.Name,
                u.Role.ToString().ToLower(),
                u.IdentityStatus.ToString().ToLower(),
                u.CreatedAt.ToString("o")))
            .ToListAsync(ct);

        var latest = transactions
            .OrderByDescending(t => t.CreatedAt)
            .Take(5)
            .Select(t => new AdminLatestTransactionDto(
                t.Id,
                $"SD-{t.Id:D6}",
                t.Title,
                t.Buyer?.Name,
                t.Vendor.Name,
                t.Amount.Amount.ToApiString(),
                t.Amount.Currency,
                t.Status.ToString().ToSnakeCase(),
                t.CreatedAt.ToString("o")))
            .ToList();

        var currency = transactions.FirstOrDefault()?.Amount.Currency ?? _platform.DefaultCurrency;

        return new AdminDashboardDto(
            transactions.Count,
            todays.Count,
            totalVolume.ToApiString(),
            escrow.ToApiString(),
            settledVolume.ToApiString(),
            commission.ToApiString(),
            todays.Sum(t => t.Amount.Amount).ToApiString(),
            settled.Count == 0 ? "0.00" : Math.Round(settledVolume / settled.Count, 2).ToApiString(),
            currency,
            successRate,
            settledToday.Count,
            totalUsers,
            newUsersThisMonth,
            pendingVerifications,
            openDisputes,
            BuildVolumeSeries(transactions, request.Range),
            BuildHourlySeries(todays),
            await BuildActivityAsync(ct),
            latest,
            newUsers);
    }

    /// <summary>Courbe de volume sur 7 jours, 30 jours ou 12 mois, trous inclus.</summary>
    private static List<SeriesPointDto> BuildVolumeSeries(List<Transaction> transactions, string range)
    {
        var series = new List<SeriesPointDto>();

        if (range == "12m")
        {
            var cursor = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(-11);
            for (var i = 0; i < 12; i++)
            {
                var from = cursor.AddMonths(i);
                var to = from.AddMonths(1);
                var slice = transactions.Where(t => t.CreatedAt >= from && t.CreatedAt < to).ToList();
                series.Add(new SeriesPointDto(from.ToString("MMM", Fr), slice.Sum(t => t.Amount.Amount).ToApiString(), slice.Count));
            }
            return series;
        }

        var days = range == "30d" ? 30 : 7;
        var start = DateTime.UtcNow.Date.AddDays(-(days - 1));
        for (var i = 0; i < days; i++)
        {
            var from = start.AddDays(i);
            var to = from.AddDays(1);
            var slice = transactions.Where(t => t.CreatedAt >= from && t.CreatedAt < to).ToList();
            series.Add(new SeriesPointDto(
                days == 7 ? from.ToString("ddd", Fr) : from.ToString("dd/MM", Fr),
                slice.Sum(t => t.Amount.Amount).ToApiString(),
                slice.Count));
        }
        return series;
    }

    /// <summary>Volume du jour par tranche de deux heures, pour la carte « Revenus aujourd'hui ».</summary>
    private static List<SeriesPointDto> BuildHourlySeries(List<Transaction> todays)
    {
        var series = new List<SeriesPointDto>();
        for (var h = 0; h < 24; h += 2)
        {
            var slice = todays.Where(t => t.CreatedAt.Hour >= h && t.CreatedAt.Hour < h + 2).ToList();
            series.Add(new SeriesPointDto($"{h:00}h", slice.Sum(t => t.Amount.Amount).ToApiString(), slice.Count));
        }
        return series;
    }

    private async Task<List<ActivityItemDto>> BuildActivityAsync(CancellationToken ct)
    {
        var logs = await _context.TransactionLogs
            .OrderByDescending(l => l.CreatedAt)
            .Take(8)
            .ToListAsync(ct);

        var ids = logs.Select(l => l.TransactionId).Distinct().ToList();
        var titles = await _context.Transactions
            .Where(t => ids.Contains(t.Id))
            .Select(t => new { t.Id, t.Title })
            .ToDictionaryAsync(t => t.Id, t => t.Title, ct);

        return logs.Select(l => new ActivityItemDto(
            l.TransactionId,
            l.Status.ToString().ToSnakeCase(),
            l.Status switch
            {
                TransactionStatus.PaymentReceived => "Paiement séquestré",
                TransactionStatus.InShipping => "Commande expédiée",
                TransactionStatus.Delivered => "Livraison confirmée",
                TransactionStatus.Closed => "Transaction terminée",
                TransactionStatus.Cancelled => "Transaction annulée",
                TransactionStatus.Dispute => "Litige ouvert",
                TransactionStatus.Resolved => "Litige tranché",
                TransactionStatus.Refunded => "Remboursement émis",
                _ => "Mise à jour"
            },
            $"{titles.GetValueOrDefault(l.TransactionId, "Transaction")} · SD-{l.TransactionId:D6}",
            l.CreatedAt.ToString("o"))).ToList();
    }
}
