using SafeDeal.Domain.Entities;

namespace SafeDeal.Domain.Interfaces.Services;

public interface ITokenService
{
    string GenerateAccessToken(User user);
    Task BlacklistTokenAsync(string token, CancellationToken ct = default);
    Task<bool> IsTokenBlacklistedAsync(string token, CancellationToken ct = default);

    /// <summary>
    /// Émet un jeton de rafraîchissement à usage unique. Il vit plus longtemps
    /// que le jeton d'accès pour qu'une session ne se termine pas brutalement au
    /// milieu d'un paiement.
    /// </summary>
    Task<string> IssueRefreshTokenAsync(int userId, CancellationToken ct = default);

    /// <summary>
    /// Consomme un jeton de rafraîchissement et renvoie l'utilisateur associé,
    /// ou null s'il est inconnu, expiré ou déjà utilisé.
    /// </summary>
    Task<int?> ConsumeRefreshTokenAsync(string refreshToken, CancellationToken ct = default);

    Task RevokeRefreshTokensAsync(int userId, CancellationToken ct = default);
}
