using SafeDeal.Domain.Entities;

namespace SafeDeal.Domain.Interfaces.Repositories;

public interface IIdentityVerificationRepository
{
    Task<IdentityVerification?> GetByUserIdAsync(int userId, CancellationToken ct = default);
    Task<IdentityVerification?> GetByIdAsync(int id, CancellationToken ct = default);
    Task AddAsync(IdentityVerification verification, CancellationToken ct = default);
    Task UpdateAsync(IdentityVerification verification, CancellationToken ct = default);
}