using MediatR;
using SafeDeal.Application.Common.Exceptions;
using SafeDeal.Domain.Interfaces.Repositories;
using SafeDeal.Domain.Interfaces.Services;

namespace SafeDeal.Application.Auth.Commands.SendTwoFactor;

public class SendTwoFactorCommandHandler : IRequestHandler<SendTwoFactorCommand>
{
    private readonly IUserRepository _users;
    private readonly IOtpService _otpService;
    private readonly IEmailService _emailService;

    public SendTwoFactorCommandHandler(IUserRepository users, IOtpService otpService, IEmailService emailService)
    {
        _users = users;
        _otpService = otpService;
        _emailService = emailService;
    }

    public async Task Handle(SendTwoFactorCommand request, CancellationToken ct)
    {
        var user = await _users.GetByIdAsync(request.UserId, ct)
            ?? throw new NotFoundException("User", request.UserId);

        if (await _otpService.IsOnCooldownAsync($"2fa_cooldown:{user.Id}", ct))
            throw new ValidationException(new Dictionary<string, string[]>
            {
                ["otp"] = ["Please wait before requesting a new OTP."]
            });

        var code = await _otpService.GenerateAndStoreAsync($"2fa:{user.Id}", ct);
        await _emailService.SendOtpAsync(user.Email, user.Name, code, ct);
    }
}