using MediatR;
using SafeDeal.Application.Admin.DTOs;
using SafeDeal.Application.Common.Models;

namespace SafeDeal.Application.Admin.Queries.GetAllUsers;

public record GetAllUsersQuery(
    int Page = 1,
    int PageSize = 20,
    string? Search = null,
    string? Role = null) : IRequest<PagedResult<AdminUserDto>>;
