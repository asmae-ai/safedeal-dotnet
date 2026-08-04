using MediatR;
using SafeDeal.Application.Auth.DTOs;
using SafeDeal.Application.Common.Exceptions;
using SafeDeal.Domain.Interfaces.Repositories;

namespace SafeDeal.Application.Auth.Queries.GetCurrentUser;

public class GetCurrentUserQueryHandler : IRequestHandler<GetCurrentUserQuery, UserDto>
{
    private readonly IUserRepository _users;
    public GetCurrentUserQueryHandler(IUserRepository users) => _users = users;

    public async Task<UserDto> Handle(GetCurrentUserQuery request, CancellationToken ct)
    {
        var user = await _users.GetByIdAsync(request.UserId, ct)
            ?? throw new NotFoundException("User", request.UserId);

        return new UserDto(
            user.Id, user.Name, user.Email,
            user.Role.ToString().ToLower(),
            user.Phone,
            user.IdentityStatus.ToString().ToLower(),
            user.ReputationScore.ToString("F2"),
            user.CreatedAt.ToString("o"));
    }
}