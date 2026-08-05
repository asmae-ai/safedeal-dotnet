using MediatR;
using Microsoft.EntityFrameworkCore;
using SafeDeal.Application.Admin.DTOs;
using SafeDeal.Application.Common.Interfaces;
using SafeDeal.Domain.Enums;

namespace SafeDeal.Application.Admin.Queries.GetPendingVerifications;

public class GetPendingVerificationsQueryHandler : IRequestHandler<GetPendingVerificationsQuery, IEnumerable<AdminVerificationDto>>
{
    private readonly IApplicationDbContext _context;
    public GetPendingVerificationsQueryHandler(IApplicationDbContext context) => _context = context;

    public async Task<IEnumerable<AdminVerificationDto>> Handle(GetPendingVerificationsQuery request, CancellationToken ct)
    {
        return await _context.IdentityVerifications
            .Include(v => v.User)
            .Where(v => v.Status == IdentityStatus.Pending)
            .OrderBy(v => v.CreatedAt)
            .Select(v => new AdminVerificationDto(
                v.Id,
                v.UserId,
                v.User.Name,
                v.User.Email,
                v.DocumentType,
                v.Status.ToString().ToLower(),
                v.CreatedAt.ToString("o")))
            .ToListAsync(ct);
    }
}