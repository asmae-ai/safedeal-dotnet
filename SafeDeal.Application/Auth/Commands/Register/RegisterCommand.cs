using MediatR;
using SafeDeal.Application.Auth.DTOs;

namespace SafeDeal.Application.Auth.Commands.Register;

public record RegisterCommand(
    string Name,
    string Email,
    string Password,
    string PasswordConfirmation,
    string Role,
    string? Phone = null) : IRequest<AuthResponseDto>;