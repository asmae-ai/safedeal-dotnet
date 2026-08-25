using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SafeDeal.Application.Common.Extensions;
using SafeDeal.Application.Common.Interfaces;
using SafeDeal.Application.Common.Options;
using SafeDeal.Application.Dashboard.DTOs;
using SafeDeal.Domain.Enums;

namespace SafeDeal.Application.Dashboard.Queries.GetVendorDashboard;

public class GetVendorDashboardQueryHandler : IRequestHandler<GetVendorDashboardQuery, VendorDashboardDto>
{
    private readonly IApplicationDbContext _context;
    private readonly PlatformOptions _platform;

    public GetVendorDashboardQueryHandler(IApplicationDbContext context, IOptions<PlatformOptions> platform)
    {
        _context = context;
        _platform = platform.Value;
    }

    public async Task<VendorDashboardDto> Handle(GetVendorDashboardQuery request, CancellationToken ct)
    {
        var transactions = await _context.Transactions
            .Include(t => t.Buyer)
            .Where(t => t.VendorId == request.VendorId)
            .ToListAsync(ct);

        // Fonds acquis : la transaction est allee a son terme en faveur du vendeur.
        var released = transactions
            .Where(t => t.Status is TransactionStatus.Closed or TransactionStatus.Resolved)
            .Sum(t => t.Amount.Amount);

        // Fonds encaisses mais encore bloques : le vendeur ne peut pas encore en disposer.
        var inEscrow = transactions
            .Where(t => t.Status is TransactionStatus.PaymentReceived
                                 or TransactionStatus.InShipping
                                 or TransactionStatus.Delivered
                                 or TransactionStatus.Dispute)
            .Sum(t => t.Amount.Amount);

        var refunded = transactions
            .Where(t => t.Status == TransactionStatus.Refunded)
            .Sum(t => t.Amount.Amount);

        var commission = Math.Round(released * _platform.CommissionRate, 2);

        // Le taux de reussite ne se calcule que sur les transactions terminees :
        // celles encore en cours ne sont ni un succes ni un echec.
        var finished = transactions.Count(t => t.Status is TransactionStatus.Closed
                                                        or TransactionStatus.Resolved
                                                        or TransactionStatus.Refunded
                                                        or TransactionStatus.Cancelled);
        var completed = transactions.Count(t => t.Status is TransactionStatus.Closed or TransactionStatus.Resolved);
        var successRate = finished == 0 ? 0 : Math.Round(completed * 100.0 / finished, 1);

        var salesSeries = BuildMonthlySeries(
            transactions.Where(t => t.Status is TransactionStatus.Closed or TransactionStatus.Resolved));

        var ordersToProcess = transactions
            .Where(t => t.Status is TransactionStatus.PaymentReceived
                                 or TransactionStatus.InShipping
                                 or TransactionStatus.Delivered)
            .OrderBy(t => t.Status == TransactionStatus.PaymentReceived ? 0 : 1)
            .ThenByDescending(t => t.UpdatedAt)
            .Take(5)
            .Select(t => new OrderToProcessDto(
                t.Id, t.SecureToken, t.Title,
                t.Amount.Amount.ToApiString(), t.Amount.Currency,
                t.Status.ToString().ToSnakeCase(),
                t.TrackingNumber, t.Carrier,
                t.Buyer?.Name,
                t.CreatedAt.ToString("o")))
            .ToList();

        var currency = transactions.FirstOrDefault()?.Amount.Currency ?? _platform.DefaultCurrency;

        return new VendorDashboardDto(
            released.ToApiString(),
            (released - commission).ToApiString(),
            commission.ToApiString(),
            inEscrow.ToApiString(),
            refunded.ToApiString(),
            currency,
            transactions.Count,
            transactions.Count(t => t.Status == TransactionStatus.PaymentReceived),
            transactions.Count(t => t.Status == TransactionStatus.InShipping),
            transactions.Count(t => t.Status == TransactionStatus.Dispute),
            successRate,
            completed,
            finished,
            salesSeries,
            ordersToProcess,
            await BuildActivityAsync(transactions.Select(t => t.Id).ToList(), ct));
    }

    /// <summary>Douze mois glissants, y compris les mois sans vente pour que la courbe reste lisible.</summary>
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
            ActivityLabel(l.Status),
            titles.GetValueOrDefault(l.TransactionId, "Transaction"),
            l.CreatedAt.ToString("o"))).ToList();
    }

    private static string ActivityLabel(TransactionStatus status) => status switch
    {
        TransactionStatus.PaymentReceived => "Paiement reçu",
        TransactionStatus.InShipping => "Commande expédiée",
        TransactionStatus.Delivered => "Réception confirmée",
        TransactionStatus.Closed => "Transaction terminée",
        TransactionStatus.Cancelled => "Transaction annulée",
        TransactionStatus.Dispute => "Litige ouvert",
        TransactionStatus.Resolved => "Litige résolu",
        TransactionStatus.Refunded => "Transaction remboursée",
        _ => "Mise à jour"
    };
}
