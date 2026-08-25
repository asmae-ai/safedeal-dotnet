using MediatR;
using Microsoft.EntityFrameworkCore;
using SafeDeal.Application.Admin.DTOs;
using SafeDeal.Application.Common.Interfaces;
using SafeDeal.Domain.Enums;

namespace SafeDeal.Application.Admin.Queries.GetIdentityStats;

public class GetIdentityStatsQueryHandler : IRequestHandler<GetIdentityStatsQuery, AdminQueueStatsDto>
{
    private readonly IApplicationDbContext _context;
    public GetIdentityStatsQueryHandler(IApplicationDbContext context) => _context = context;

    public async Task<AdminQueueStatsDto> Handle(GetIdentityStatsQuery request, CancellationToken ct)
    {
        var monthStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        var total = await _context.IdentityVerifications.CountAsync(ct);
        var pending = await _context.IdentityVerifications.CountAsync(v => v.Status == IdentityStatus.Pending, ct);
        var approved = await _context.IdentityVerifications.CountAsync(v => v.Status == IdentityStatus.Approved, ct);
        var rejected = await _context.IdentityVerifications.CountAsync(v => v.Status == IdentityStatus.Rejected, ct);
        var newThisMonth = await _context.IdentityVerifications.CountAsync(v => v.CreatedAt >= monthStart, ct);

        var rate = total == 0 ? 0 : Math.Round((approved + rejected) * 100.0 / total, 1);

        return new AdminQueueStatsDto(total, pending, approved, rejected, newThisMonth, rate);
    }
}
