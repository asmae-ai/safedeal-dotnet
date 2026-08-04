using MediatR;
using SafeDeal.Application.Auth.DTOs;

namespace SafeDeal.Application.Auth.Queries.GetCurrentUser;

public record GetCurrentUserQuery(int UserId) : IRequest<UserDto>;