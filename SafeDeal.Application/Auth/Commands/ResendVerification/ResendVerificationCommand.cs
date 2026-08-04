using MediatR;

namespace SafeDeal.Application.Auth.Commands.ResendVerification;

public record ResendVerificationCommand(int UserId) : IRequest;