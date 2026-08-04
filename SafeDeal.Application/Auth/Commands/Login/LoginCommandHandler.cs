using MediatR;
using SafeDeal.Application.Auth.DTOs;
using SafeDeal.Application.Common.Exceptions;
using SafeDeal.Domain.Interfaces.Repositories;
using SafeDeal.Domain.Interfaces.Services;

namespace SafeDeal.Application.Auth.Commands.Login;

public class LoginCommandHandler : IRequestHandler<LoginCommand, AuthResponseDto>
{
    private readonly IUserRepository _users;
    private readonly ITokenService _tokenService;

    public LoginCommandHandler(IUserRepository users, ITokenService tokenService)
    {
        _users = users;
        _tokenService = tokenService;
    }

    public async Task<AuthResponseDto> Handle(LoginCommand request, CancellationToken ct)
    {
        var user = await _users.GetByEmailAsync(request.Email, ct)
            ?? throw new UnauthorizedException("Invalid credentials.");

        if (!user.IsEmailVerified)
            throw new ForbiddenException("Please verify your email before logging in.");

        if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            throw new UnauthorizedException("Invalid credentials.");

        if (!user.IsActive)
            throw new ForbiddenException("Your account has been deactivated.");

        var token = _tokenService.GenerateAccessToken(user);

        return new AuthResponseDto(token, new UserDto(
            user.Id, user.Name, user.Email,
            user.Role.ToString().ToLower(),
            user.Phone,
            user.IdentityStatus.ToString().ToLower(),
            user.ReputationScore.ToString("F2"),
            user.CreatedAt.ToString("o")));
    }
}