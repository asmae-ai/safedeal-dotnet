using MediatR;
using SafeDeal.Application.Common.Exceptions;
using SafeDeal.Domain.Interfaces.Repositories;
using SafeDeal.Domain.Interfaces.Services;

namespace SafeDeal.Application.Auth.Commands.VerifyEmail;

public class VerifyEmailCommandHandler : IRequestHandler<VerifyEmailCommand>
{
    private readonly IUserRepository _users;
    private readonly IOtpService _otpService;

    public VerifyEmailCommandHandler(IUserRepository users, IOtpService otpService)
    {
        _users = users;
        _otpService = otpService;
    }

    public async Task Handle(VerifyEmailCommand request, CancellationToken ct)
    {
        var user = await _users.GetByIdAsync(request.UserId, ct)
            ?? throw new NotFoundException("User", request.UserId);

        var isValid = await _otpService.ValidateAsync($"email_verify:{user.Id}", request.Code, ct);
        if (!isValid) throw new ValidationException(new Dictionary<string, string[]>
        {
            ["code"] = ["Invalid or expired verification code."]
        });

        user.VerifyEmail();
        await _users.UpdateAsync(user, ct);
        await _otpService.InvalidateAsync($"email_verify:{user.Id}", ct);
    }
}