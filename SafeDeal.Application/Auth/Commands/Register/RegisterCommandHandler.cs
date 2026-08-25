using MediatR;
using SafeDeal.Application.Common.Extensions;
using SafeDeal.Application.Auth.DTOs;
using SafeDeal.Application.Common.Exceptions;
using SafeDeal.Domain.Entities;
using SafeDeal.Domain.Enums;
using SafeDeal.Domain.Interfaces.Repositories;
using SafeDeal.Domain.Interfaces.Services;

namespace SafeDeal.Application.Auth.Commands.Register;

public class RegisterCommandHandler : IRequestHandler<RegisterCommand, AuthResponseDto>
{
    private readonly IUserRepository _users;
    private readonly ITokenService _tokenService;
    private readonly IOtpService _otpService;
    private readonly IEmailService _emailService;

    public RegisterCommandHandler(
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

    public async Task<AuthResponseDto> Handle(RegisterCommand request, CancellationToken ct)
    {
        if (await _users.ExistsByEmailAsync(request.Email, ct))
            throw new ValidationException(new Dictionary<string, string[]>
            {
                ["email"] = ["The email has already been taken."]
            });

        var role = request.Role == "vendor" ? UserRole.Vendor : UserRole.Buyer;
        var hash = BCrypt.Net.BCrypt.HashPassword(request.Password);
        var user = User.Create(request.Name, request.Email, hash, role, request.Phone);

        await _users.AddAsync(user, ct);

        var code = await _otpService.GenerateAndStoreAsync($"email_verify:{user.Id}", ct);
        await _emailService.SendVerificationCodeAsync(user.Email, user.Name, code, ct);

        var token = _tokenService.GenerateAccessToken(user);
        return new AuthResponseDto(token, UserDto.From(user));
    }
}
