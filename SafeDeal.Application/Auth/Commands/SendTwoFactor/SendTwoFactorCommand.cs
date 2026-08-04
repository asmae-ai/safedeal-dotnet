using MediatR;

namespace SafeDeal.Application.Auth.Commands.SendTwoFactor;

public record SendTwoFactorCommand(int UserId) : IRequest;