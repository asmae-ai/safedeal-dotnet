using SafeDeal.Application.Common.Extensions;
using SafeDeal.Domain.Entities;

namespace SafeDeal.Application.Auth.DTOs;

public record UserDto(
    int Id,
    string Name,
    string Email,
    string Role,
    string? Phone,
    string IdentityStatus,
    string ReputationScore,
    string CreatedAt,
    string? AvatarPath,
    bool IsEmailVerified,
    bool TwoFactorEnabled)
{
    /// <summary>
    /// Projection unique de l'utilisateur vers le contrat d'API. Elle était
    /// dupliquée dans cinq handlers, avec le risque de diverger à chaque ajout
    /// de champ.
    /// </summary>
    public static UserDto From(User user) => new(
        user.Id,
        user.Name,
        user.Email,
        user.Role.ToString().ToLower(),
        user.Phone,
        user.IdentityStatus.ToString().ToLower(),
        user.ReputationScore.ToApiString(),
        user.CreatedAt.ToString("o"),
        user.AvatarPath,
        user.IsEmailVerified,
        user.TwoFactorEnabled);
}
