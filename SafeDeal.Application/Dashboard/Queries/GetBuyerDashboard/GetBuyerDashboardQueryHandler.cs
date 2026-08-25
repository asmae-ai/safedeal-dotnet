using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SafeDeal.Application.Common.Extensions;
using SafeDeal.Application.Common.Interfaces;
using SafeDeal.Application.Common.Options;
using SafeDeal.Application.Dashboard.DTOs;
using SafeDeal.Domain.Enums;

namespace SafeDeal.Application.Dashboard.Queries.GetBuyerDashboard;

public class GetBuyerDashboardQueryHandler : IRequestHandler<GetBuyerDashboardQuery, BuyerDashboardDto>
{
    private static readonly TransactionStatus[] ActiveStatuses =
    [
        TransactionStatus.PendingPayment,
        TransactionStatus.PaymentReceived,
        TransactionStatus.InShipping,
        TransactionStatus.Delivered
    ];

    private readonly IApplicationDbContext _context;
    private readonly PlatformOptions _platform;

    public GetBuyerDashboardQueryHandler(IApplicationDbContext context, IOptions<PlatformOptions> platform)
    {
        _context = context;
        _platform = platform.Value;
    }

    public async Task<BuyerDashboardDto> Handle(GetBuyerDashboardQuery request, CancellationToken ct)
    {
        var transactions = await _context.Transactions
            .Where(t => t.BuyerId == request.BuyerId)
            .ToListAsync(ct);

        // Ce que l'acheteur a reellement depense : les commandes menees a leur terme,
        // remboursements exclus.
        var spent = transactions
            .Where(t => t.Status is TransactionStatus.Closed or TransactionStatus.Resolved)
            .Sum(t => t.Amount.Amount);

        var inEscrow = transactions
            .Where(t => t.Status is TransactionStatus.PaymentReceived
                                 or TransactionStatus.InShipping
                                 or TransactionStatus.Delivered
                                 or TransactionStatus.Dispute)
            .Sum(t => t.Amount.Amount);

        var refunded = transactions
            .Where(t => t.Status == TransactionStatus.Refunded)
            .Sum(t => t.Amount.Amount);

        var unread = await _context.Notifications
            .CountAsync(n => n.UserId == request.BuyerId && n.ReadAt == null, ct);

        var spendingSeries = BuildMonthlySeries(
            transactions.Where(t => t.Status is TransactionStatus.Closed or TransactionStatus.Resolved));

        var currency = transactions.FirstOrDefault()?.Amount.Currency ?? _platform.DefaultCurrency;

        return new BuyerDashboardDto(
            spent.ToApiString(),
            inEscrow.ToApiString(),
            refunded.ToApiString(),
            currency,
            transactions.Count,
            transactions.Count(t => ActiveStatuses.Contains(t.Status)),
            transactions.Count(t => t.Status is TransactionStatus.Closed or TransactionStatus.Resolved),
            transactions.Count(t => t.Status == TransactionStatus.Dispute),
            unread,
            spendingSeries,
            await BuildActivityAsync(transactions.Select(t => t.Id).ToList(), ct));
    }

    private static List<SeriesPointDto> BuildMonthlySeries(IEnumerable<Domain.Entities.Transaction> source)
    {
        var items = source.ToList();
        var series = new List<SeriesPointDto>();
        var cursor = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(-11);

        for (var i = 0; i < 12; i++)
        {
            var month = cursor.AddMonths(i);
            var next = month.AddMonths(1);
            var inMonth = items.Where(t => t.UpdatedAt >= month && t.UpdatedAt < next).ToList();

            series.Add(new SeriesPointDto(
                month.ToString("MMM", System.Globalization.CultureInfo.GetCultureInfo("fr-FR")),
                inMonth.Sum(t => t.Amount.Amount).ToApiString(),
                inMonth.Count));
        }

        return series;
    }

    private async Task<List<ActivityItemDto>> BuildActivityAsync(List<int> transactionIds, CancellationToken ct)
    {
        if (transactionIds.Count == 0) return [];

        var logs = await _context.TransactionLogs
            .Where(l => transactionIds.Contains(l.TransactionId))
            .OrderByDescending(l => l.CreatedAt)
            .Take(8)
            .ToListAsync(ct);

        var titles = await _context.Transactions
            .Where(t => transactionIds.Contains(t.Id))
            .Select(t => new { t.Id, t.Title })
            .ToDictionaryAsync(t => t.Id, t => t.Title, ct);

        return logs.Select(l => new ActivityItemDto(
            l.TransactionId,
            l.Status.ToString().ToSnakeCase(),
            l.Status switch
            {
                TransactionStatus.PaymentReceived => "Paiement sécurisé",
                TransactionStatus.InShipping => "Commande expédiée",
                TransactionStatus.Delivered => "Livraison confirmée",
                TransactionStatus.Closed => "Commande terminée",
                TransactionStatus.Cancelled => "Commande annulée",
                TransactionStatus.Dispute => "Litige ouvert",
                TransactionStatus.Resolved => "Litige résolu",
                TransactionStatus.Refunded => "Remboursement effectué",
                _ => "Mise à jour"
            },
            titles.GetValueOrDefault(l.TransactionId, "Commande"),
            l.CreatedAt.ToString("o"))).ToList();
    }
}
