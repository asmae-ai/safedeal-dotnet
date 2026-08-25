using MediatR;
using SafeDeal.Application.Auth.DTOs;
using SafeDeal.Application.Common.Exceptions;
using SafeDeal.Domain.Interfaces.Repositories;
using SafeDeal.Domain.Interfaces.Services;

namespace SafeDeal.Application.Auth.Commands.VerifyTwoFactor;

public class VerifyTwoFactorCommandHandler : IRequestHandler<VerifyTwoFactorCommand, AuthResponseDto>
{
    private readonly IUserRepository _users;
    private readonly IOtpService _otpService;
    private readonly ITokenService _tokenService;

    public VerifyTwoFactorCommandHandler(
        IUserRepository users,
        IOtpService otpService,
        ITokenService tokenService)
    {
        _users = users;
        _otpService = otpService;
        _tokenService = tokenService;
    }

    public async Task<AuthResponseDto> Handle(VerifyTwoFactorCommand request, CancellationToken ct)
    {
        var user = await _users.GetByEmailAsync(request.Email, ct)
            ?? throw new UnauthorizedException("Invalid or expired code.");

        var isValid = await _otpService.ValidateAsync($"2fa:{user.Id}", request.Code, ct);
        if (!isValid)
            throw new UnauthorizedException("Invalid or expired code.");

        // Un code ne sert qu'une fois : il est invalidé dès qu'il a servi.
        await _otpService.InvalidateAsync($"2fa:{user.Id}", ct);

        return new AuthResponseDto(_tokenService.GenerateAccessToken(user), UserDto.From(user));
    }
}
