using MediatR;
using SafeDeal.Application.Common.Exceptions;
using SafeDeal.Domain.Interfaces.Repositories;
using SafeDeal.Domain.Interfaces.Services;

namespace SafeDeal.Application.Auth.Commands.ResendVerification;

public class ResendVerificationCommandHandler : IRequestHandler<ResendVerificationCommand>
{
    private readonly IUserRepository _users;
    private readonly IOtpService _otpService;
    private readonly IEmailService _emailService;

    public ResendVerificationCommandHandler(IUserRepository users, IOtpService otpService, IEmailService emailService)
    {
        _users = users;
        _otpService = otpService;
        _emailService = emailService;
    }

    public async Task Handle(ResendVerificationCommand request, CancellationToken ct)
    {
        var user = await _users.GetByIdAsync(request.UserId, ct)
            ?? throw new NotFoundException("User", request.UserId);

        if (user.IsEmailVerified)
            throw new ValidationException(new Dictionary<string, string[]>
            {
                ["email"] = ["Email is already verified."]
            });

        var code = await _otpService.GenerateAndStoreAsync($"email_verify:{user.Id}", ct);
        await _emailService.SendVerificationCodeAsync(user.Email, user.Name, code, ct);
    }
}