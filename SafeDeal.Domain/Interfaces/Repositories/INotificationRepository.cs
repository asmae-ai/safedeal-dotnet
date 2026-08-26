using SafeDeal.Domain.Entities;

namespace SafeDeal.Domain.Interfaces.Repositories;

public interface INotificationRepository
{
    /// <param name="page">Nul, la liste complete est rendue.</param>
    Task<(IEnumerable<Notification> Items, int Total)> GetByUserIdAsync(
        int userId, int? page, int pageSize, CancellationToken ct = default);
    Task<Notification?> GetByIdAsync(int id, CancellationToken ct = default);
    Task AddAsync(Notification notification, CancellationToken ct = default);
    Task UpdateAsync(Notification notification, CancellationToken ct = default);
    Task MarkAllAsReadAsync(int userId, CancellationToken ct = default);
}