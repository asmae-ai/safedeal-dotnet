using MediatR;

namespace SafeDeal.Application.Auth.Commands.Logout;

public record LogoutCommand(string Token) : IRequest;