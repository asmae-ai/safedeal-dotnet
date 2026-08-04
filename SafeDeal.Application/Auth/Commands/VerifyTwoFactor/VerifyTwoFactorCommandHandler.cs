using MediatR;
using SafeDeal.Application.Common.Exceptions;
using SafeDeal.Domain.Interfaces.Services;

namespace SafeDeal.Application.Auth.Commands.VerifyTwoFactor;

public class VerifyTwoFactorCommandHandler : IRequestHandler<VerifyTwoFactorCommand>
{
    private readonly IOtpService _otpService;
    public VerifyTwoFactorCommandHandler(IOtpService otpService) => _otpService = otpService;

    public async Task Handle(VerifyTwoFactorCommand request, CancellationToken ct)
    {
        var isValid = await _otpService.ValidateAsync($"2fa:{request.UserId}", request.Code, ct);
        if (!isValid) throw new ValidationException(new Dictionary<string, string[]>
        {
            ["code"] = ["Invalid or expired OTP code."]
        });
        await _otpService.InvalidateAsync($"2fa:{request.UserId}", ct);
    }
}