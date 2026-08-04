using MediatR;

namespace SafeDeal.Application.Auth.Commands.VerifyTwoFactor;

public record VerifyTwoFactorCommand(int UserId, string Code) : IRequest;