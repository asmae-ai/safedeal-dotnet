using MediatR;
using SafeDeal.Application.Auth.DTOs;

namespace SafeDeal.Application.Auth.Commands.Login;

public record LoginCommand(string Email, string Password) : IRequest<AuthResponseDto>;