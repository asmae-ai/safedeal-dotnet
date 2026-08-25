using MediatR;

namespace SafeDeal.Application.Auth.Commands.SetTwoFactor;

public record SetTwoFactorCommand(int UserId, bool Enabled) : IRequest;
