using MediatR;
using SafeDeal.Domain.Interfaces.Repositories;
using SafeDeal.Domain.Interfaces.Services;

namespace SafeDeal.Application.Auth.Commands.ForgotPassword;

public class ForgotPasswordCommandHandler : IRequestHandler<ForgotPasswordCommand>
{
    private readonly IUserRepository _users;
    private readonly IOtpService _otpService;
    private readonly IEmailService _emailService;

    public ForgotPasswordCommandHandler(IUserRepository users, IOtpService otpService, IEmailService emailService)
    {
        _users = users;
        _otpService = otpService;
        _emailService = emailService;
    }

    public async Task Handle(ForgotPasswordCommand request, CancellationToken ct)
    {
        var user = await _users.GetByEmailAsync(request.Email, ct);
        if (user is null) return; // Sécurité : même réponse si email inconnu

        var token = await _otpService.GenerateAndStoreAsync($"pwd_reset:{user.Email}", ct);
        await _emailService.SendPasswordResetAsync(user.Email, user.Name, token, ct);
    }
}