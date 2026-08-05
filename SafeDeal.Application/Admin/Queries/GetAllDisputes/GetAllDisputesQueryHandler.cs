using MediatR;
using Microsoft.EntityFrameworkCore;
using SafeDeal.Application.Common.Interfaces;
using SafeDeal.Application.Disputes.DTOs;

namespace SafeDeal.Application.Admin.Queries.GetAllDisputes;

public class GetAllDisputesQueryHandler : IRequestHandler<GetAllDisputesQuery, IEnumerable<DisputeDto>>
{
    private readonly IApplicationDbContext _context;
    public GetAllDisputesQueryHandler(IApplicationDbContext context) => _context = context;

    public async Task<IEnumerable<DisputeDto>> Handle(GetAllDisputesQuery request, CancellationToken ct)
    {
        return await _context.Disputes
            .Include(d => d.OpenedBy)
            .OrderByDescending(d => d.CreatedAt)
            .Select(d => new DisputeDto(
                d.Id,
                d.Category,
                d.Description,
                d.Status.ToString().ToLower(),
                d.CreatedAt.ToString("o"),
                new UserOpenedByDto(d.OpenedBy.Id, d.OpenedBy.Name, d.OpenedBy.Email),
                new List<EvidenceDto>()))
            .ToListAsync(ct);
    }
}