using MediatR;
using Microsoft.EntityFrameworkCore;
using SafeDeal.Application.Admin.DTOs;
using SafeDeal.Application.Common.Interfaces;
using SafeDeal.Application.Common.Models;
using SafeDeal.Domain.Enums;

namespace SafeDeal.Application.Admin.Queries.GetAuditLogs;

public class GetAuditLogsQueryHandler : IRequestHandler<GetAuditLogsQuery, PagedResult<AuditLogDto>>
{
    private readonly IApplicationDbContext _context;
    public GetAuditLogsQueryHandler(IApplicationDbContext context) => _context = context;

    public async Task<PagedResult<AuditLogDto>> Handle(GetAuditLogsQuery request, CancellationToken ct)
    {
        // AsNoTracking : le journal est immuable, rien ici ne sera reecrit.
        var query = _context.AuditLogs.AsNoTracking();

        // Un nom d'action inconnu ne filtre sur rien : mieux vaut une liste vide
        // qu'un filtre silencieusement ignore, qui laisserait croire a un journal
        // complet.
        if (!string.IsNullOrWhiteSpace(request.Action))
        {
            query = Enum.TryParse<AuditAction>(request.Action, ignoreCase: true, out var action)
                ? query.Where(a => a.Action == action)
                : query.Where(_ => false);
        }

        if (request.UserId is int userId)
            query = query.Where(a => a.UserId == userId);

        if (!string.IsNullOrWhiteSpace(request.EntityType))
            query = query.Where(a => a.EntityType == request.EntityType);

        if (request.EntityId is int entityId)
            query = query.Where(a => a.EntityId == entityId);

        if (request.SucceededOnly is bool succeeded)
            query = query.Where(a => a.Succeeded == succeeded);

        var total = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(a => a.CreatedAt)
            .ThenByDescending(a => a.Id)
            .Skip((request.SafePage - 1) * request.SafePageSize)
            .Take(request.SafePageSize)
            .ToListAsync(ct);

        var dtos = items.Select(a => new AuditLogDto(
            a.Id,
            a.Action.ToString(),
            a.UserId,
            a.Subject,
            a.EntityType,
            a.EntityId,
            a.Succeeded,
            a.FailureReason,
            a.IpAddress,
            a.UserAgent,
            a.Metadata,
            a.CreatedAt.ToString("o"))).ToList();

        return new PagedResult<AuditLogDto>(dtos, request.SafePage, Paging.LastPage(total, request.SafePageSize), total);
    }
}
