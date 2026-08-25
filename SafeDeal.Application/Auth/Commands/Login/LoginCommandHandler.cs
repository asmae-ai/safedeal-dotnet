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
    private readonly IOtpService _otpService;
    private readonly IEmailService _emailService;

    public LoginCommandHandler(
        IUserRepository users,
        ITokenService tokenService,
        IOtpService otpService,
        IEmailService emailService)
    {
        _users = users;
        _tokenService = tokenService;
        _otpService = otpService;
        _emailService = emailService;
    }

    public async Task<AuthResponseDto> Handle(LoginCommand request, CancellationToken ct)
    {
        var user = await _users.GetByEmailAsync(request.Email, ct)
            ?? throw new UnauthorizedException("Invalid credentials.");

        // Le mot de passe est vérifié avant tout autre motif de refus : sinon la réponse
        // révélerait l'existence d'un compte à qui ne connaît pas le mot de passe.
        if (!user.VerifyPassword(request.Password))
            throw new UnauthorizedException("Invalid credentials.");

        if (!user.IsActive)
            throw new ForbiddenException("Your account has been deactivated.");

        if (!user.IsEmailVerified)
            throw new EmailNotVerifiedException();

        if (user.TwoFactorEnabled)
        {
            var otp = await _otpService.GenerateAndStoreAsync($"2fa:{user.Id}", ct);
            await _emailService.SendOtpAsync(user.Email, user.Name, otp, ct);
            return new AuthResponseDto(null, null, RequiresTwoFactor: true);
        }

        return new AuthResponseDto(
            _tokenService.GenerateAccessToken(user),
            UserDto.From(user),
            RefreshToken: await _tokenService.IssueRefreshTokenAsync(user.Id, ct));
    }
}
