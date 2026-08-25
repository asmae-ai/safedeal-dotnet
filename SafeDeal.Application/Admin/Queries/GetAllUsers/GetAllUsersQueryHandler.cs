using MediatR;
using Microsoft.EntityFrameworkCore;
using SafeDeal.Application.Admin.DTOs;
using SafeDeal.Application.Common.Extensions;
using SafeDeal.Application.Common.Interfaces;
using SafeDeal.Application.Common.Models;
using SafeDeal.Domain.Enums;

namespace SafeDeal.Application.Admin.Queries.GetAllUsers;

public class GetAllUsersQueryHandler : IRequestHandler<GetAllUsersQuery, PagedResult<AdminUserDto>>
{
    private readonly IApplicationDbContext _context;
    public GetAllUsersQueryHandler(IApplicationDbContext context) => _context = context;

    public async Task<PagedResult<AdminUserDto>> Handle(GetAllUsersQuery request, CancellationToken ct)
    {
        var query = _context.Users.AsQueryable();

        // Le filtrage se fait en base : filtrer la seule page deja chargee cote
        // client ne trouvait jamais un utilisateur des pages suivantes.
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim().ToLower();
            query = query.Where(u => u.Name.ToLower().Contains(term) || u.Email.ToLower().Contains(term));
        }

        if (!string.IsNullOrWhiteSpace(request.Role)
            && Enum.TryParse<UserRole>(request.Role, ignoreCase: true, out var role))
        {
            query = query.Where(u => u.Role == role);
        }

        var total = await query.CountAsync(ct);
        var lastPage = total == 0 ? 1 : (int)Math.Ceiling(total / (double)request.PageSize);

        var items = await query
            .OrderByDescending(u => u.CreatedAt)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(ct);

        var dtos = items.Select(u => new AdminUserDto(
            u.Id, u.Name, u.Email,
            u.Role.ToString().ToLower(),
            u.IdentityStatus.ToString().ToLower(),
            u.ReputationScore.ToApiString(),
            u.IsEmailVerified,
            u.IsActive,
            u.CreatedAt.ToString("o"))).ToList();

        return new PagedResult<AdminUserDto>(dtos, request.Page, lastPage, total);
    }
}
