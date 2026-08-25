using MediatR;
using SafeDeal.Application.Auth.DTOs;
using SafeDeal.Application.Common.Exceptions;
using SafeDeal.Domain.Interfaces.Repositories;
using SafeDeal.Domain.Interfaces.Services;

namespace SafeDeal.Application.Auth.Commands.RefreshToken;

public class RefreshTokenCommandHandler : IRequestHandler<RefreshTokenCommand, AuthResponseDto>
{
    private readonly ITokenService _tokens;
    private readonly IUserRepository _users;

    public RefreshTokenCommandHandler(ITokenService tokens, IUserRepository users)
    {
        _tokens = tokens;
        _users = users;
    }

    public async Task<AuthResponseDto> Handle(RefreshTokenCommand request, CancellationToken ct)
    {
        var userId = await _tokens.ConsumeRefreshTokenAsync(request.RefreshToken, ct)
            ?? throw new UnauthorizedException("Invalid or expired refresh token.");

        var user = await _users.GetByIdAsync(userId, ct)
            ?? throw new UnauthorizedException("Invalid or expired refresh token.");

        // Un compte desactive entre-temps ne doit pas pouvoir prolonger sa session.
        if (!user.IsActive)
            throw new ForbiddenException("Your account has been deactivated.");

        // Rotation : chaque rafraichissement emet un nouveau jeton et invalide
        // l'ancien, pour qu'un jeton intercepte ne serve qu'une fois.
        return new AuthResponseDto(
            _tokens.GenerateAccessToken(user),
            UserDto.From(user),
            RefreshToken: await _tokens.IssueRefreshTokenAsync(user.Id, ct));
    }
}
