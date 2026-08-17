using SafeDeal.Domain.Entities;

namespace SafeDeal.Domain.Interfaces.Repositories;

public interface IDisputeRepository
{
    Task<Dispute?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<Dispute?> GetByTransactionIdAsync(int transactionId, CancellationToken ct = default);
    Task AddAsync(Dispute dispute, CancellationToken ct = default);
    Task UpdateAsync(Dispute dispute, CancellationToken ct = default);
}