namespace SafeDeal.Application.Auth.DTOs;

/// <summary>
/// Réponse de connexion. Quand la 2FA est active, aucun jeton n'est délivré :
/// le client doit d'abord présenter le code reçu par e-mail.
/// </summary>
public record AuthResponseDto(string? Token, UserDto? User, bool RequiresTwoFactor = false);
