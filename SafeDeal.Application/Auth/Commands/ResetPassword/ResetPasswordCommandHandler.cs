using MediatR;
using SafeDeal.Application.Common.Exceptions;
using SafeDeal.Domain.Interfaces.Repositories;
using SafeDeal.Domain.Interfaces.Services;

namespace SafeDeal.Application.Auth.Commands.ResetPassword;

public class ResetPasswordCommandHandler : IRequestHandler<ResetPasswordCommand>
{
    private readonly IUserRepository _users;
    private readonly IOtpService _otpService;

    public ResetPasswordCommandHandler(IUserRepository users, IOtpService otpService)
    {
        _users = users;
        _otpService = otpService;
    }

    public async Task Handle(ResetPasswordCommand request, CancellationToken ct)
    {
        var user = await _users.GetByEmailAsync(request.Email, ct)
            ?? throw new NotFoundException("User", request.Email);

        var isValid = await _otpService.ValidateAsync($"pwd_reset:{user.Email}", request.Token, ct);
        if (!isValid) throw new ValidationException(new Dictionary<string, string[]>
        {
            ["token"] = ["Invalid or expired reset token."]
        });

        user.UpdatePasswordHash(BCrypt.Net.BCrypt.HashPassword(request.Password));
        await _users.UpdateAsync(user, ct);
        await _otpService.InvalidateAsync($"pwd_reset:{user.Email}", ct);
    }
}