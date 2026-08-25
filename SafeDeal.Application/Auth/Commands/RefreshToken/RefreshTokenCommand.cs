using MediatR;
using SafeDeal.Application.Auth.DTOs;

namespace SafeDeal.Application.Auth.Commands.RefreshToken;

public record RefreshTokenCommand(string RefreshToken) : IRequest<AuthResponseDto>;
