using MediatR;
using Microsoft.EntityFrameworkCore;
using SafeDeal.Application.Admin.DTOs;
using SafeDeal.Application.Common.Interfaces;
using SafeDeal.Domain.Enums;

namespace SafeDeal.Application.Admin.Queries.GetDisputeStats;

public class GetDisputeStatsQueryHandler : IRequestHandler<GetDisputeStatsQuery, AdminQueueStatsDto>
{
    private readonly IApplicationDbContext _context;
    public GetDisputeStatsQueryHandler(IApplicationDbContext context) => _context = context;

    public async Task<AdminQueueStatsDto> Handle(GetDisputeStatsQuery request, CancellationToken ct)
    {
        var monthStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        var total = await _context.Disputes.CountAsync(ct);
        var open = await _context.Disputes.CountAsync(d => d.Status == DisputeStatus.Open, ct);
        var underReview = await _context.Disputes.CountAsync(d => d.Status == DisputeStatus.UnderReview, ct);
        var settled = await _context.Disputes
            .CountAsync(d => d.Status == DisputeStatus.Resolved || d.Status == DisputeStatus.Closed, ct);
        var newThisMonth = await _context.Disputes.CountAsync(d => d.CreatedAt >= monthStart, ct);

        var rate = total == 0 ? 0 : Math.Round(settled * 100.0 / total, 1);

        // Pour la file des litiges : Pending = ouverts, Approved = en cours d'examen,
        // Rejected = tranchés. Mêmes compteurs, libellés propres à l'écran.
        return new AdminQueueStatsDto(total, open, underReview, settled, newThisMonth, rate);
    }
}
