using MediatR;
using SafeDeal.Application.Admin.DTOs;

namespace SafeDeal.Application.Admin.Queries.GetAllUsers;

public record GetAllUsersQuery(int Page = 1, int PageSize = 20) : IRequest<IEnumerable<AdminUserDto>>;