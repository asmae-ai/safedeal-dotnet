using MediatR;
using SafeDeal.Application.Auth.DTOs;

namespace SafeDeal.Application.Auth.Commands.VerifyTwoFactor;

/// <summary>
/// Seconde étape de connexion : l'utilisateur n'a pas encore de jeton, il
/// s'identifie donc par son e-mail et le code reçu.
/// </summary>
public record VerifyTwoFactorCommand(string Email, string Code) : IRequest<AuthResponseDto>;
