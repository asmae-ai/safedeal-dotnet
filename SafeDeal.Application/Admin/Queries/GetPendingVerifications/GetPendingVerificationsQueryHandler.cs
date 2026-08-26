using MediatR;
using Microsoft.EntityFrameworkCore;
using SafeDeal.Application.Admin.DTOs;
using SafeDeal.Application.Common.Interfaces;
using SafeDeal.Application.Common.Models;
using SafeDeal.Domain.Enums;

namespace SafeDeal.Application.Admin.Queries.GetPendingVerifications;

public class GetPendingVerificationsQueryHandler : IRequestHandler<GetPendingVerificationsQuery, PagedResult<AdminVerificationDto>>
{
    private readonly IApplicationDbContext _context;
    public GetPendingVerificationsQueryHandler(IApplicationDbContext context) => _context = context;

    public async Task<PagedResult<AdminVerificationDto>> Handle(GetPendingVerificationsQuery request, CancellationToken ct)
    {
        var pending = _context.IdentityVerifications
            .Include(v => v.User)
            .Where(v => v.Status == IdentityStatus.Pending);

        var total = await pending.CountAsync(ct);

        // La file se traite du plus ancien au plus recent : personne ne doit
        // rester au fond parce que de nouveaux dossiers arrivent.
        var items = await pending
            .OrderBy(v => v.CreatedAt)
            .Slice(request)
            .Select(v => new AdminVerificationDto(
                v.Id,
                v.UserId,
                v.User.Name,
                v.User.Email,
                v.DocumentType,
                v.Status.ToString().ToLower(),
                v.CreatedAt.ToString("o"),
                v.DocumentFrontPath,
                v.SelfiePath))
            .ToListAsync(ct);

        return items.ToResult(request, total);
    }
}