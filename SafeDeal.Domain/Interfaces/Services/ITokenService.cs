using SafeDeal.Domain.Entities;

namespace SafeDeal.Domain.Interfaces.Services;

public interface ITokenService
{
    string GenerateAccessToken(User user);
    Task BlacklistTokenAsync(string token, CancellationToken ct = default);
    Task<bool> IsTokenBlacklistedAsync(string token, CancellationToken ct = default);
}