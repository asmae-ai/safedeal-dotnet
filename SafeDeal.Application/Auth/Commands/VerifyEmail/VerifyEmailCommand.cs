using MediatR;

namespace SafeDeal.Application.Auth.Commands.VerifyEmail;

public record VerifyEmailCommand(int UserId, string Code) : IRequest;