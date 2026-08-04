using SafeDeal.Domain.Entities;

namespace SafeDeal.Domain.Interfaces.Repositories;

public interface ITransactionRepository
{
    Task<Transaction?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<Transaction?> GetByTokenAsync(string token, CancellationToken ct = default);
    Task<(IEnumerable<Transaction> Items, int Total)> GetByUserIdAsync(int userId, int page, int pageSize, CancellationToken ct = default);
    Task AddAsync(Transaction transaction, CancellationToken ct = default);
    Task UpdateAsync(Transaction transaction, CancellationToken ct = default);
}