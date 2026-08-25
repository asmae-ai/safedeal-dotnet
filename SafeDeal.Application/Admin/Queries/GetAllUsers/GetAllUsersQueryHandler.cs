using MediatR;
using SafeDeal.Application.Common.Extensions;
using Microsoft.EntityFrameworkCore;
using SafeDeal.Application.Admin.DTOs;
using SafeDeal.Application.Common.Interfaces;

namespace SafeDeal.Application.Admin.Queries.GetAllUsers;

public class GetAllUsersQueryHandler : IRequestHandler<GetAllUsersQuery, IEnumerable<AdminUserDto>>
{
    private readonly IApplicationDbContext _context;
    public GetAllUsersQueryHandler(IApplicationDbContext context) => _context = context;

    public async Task<IEnumerable<AdminUserDto>> Handle(GetAllUsersQuery request, CancellationToken ct)
    {
        return await _context.Users
            .OrderByDescending(u => u.CreatedAt)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(u => new AdminUserDto(
                u.Id, u.Name, u.Email,
                u.Role.ToString().ToLower(),
                u.IdentityStatus.ToString().ToLower(),
                u.ReputationScore.ToApiString(),
                u.IsEmailVerified,
                u.IsActive,
                u.CreatedAt.ToString("o")))
            .ToListAsync(ct);
    }
}