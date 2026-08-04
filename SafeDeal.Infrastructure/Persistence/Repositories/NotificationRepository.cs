using Microsoft.EntityFrameworkCore;
using SafeDeal.Domain.Entities;
using SafeDeal.Domain.Interfaces.Repositories;

namespace SafeDeal.Infrastructure.Persistence.Repositories;

public class NotificationRepository : INotificationRepository
{
    private readonly AppDbContext _context;
    public NotificationRepository(AppDbContext context) => _context = context;

    public async Task<IEnumerable<Notification>> GetByUserIdAsync(int userId, CancellationToken ct = default)
        => await _context.Notifications
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAt)
            .ToListAsync(ct);

    public async Task<Notification?> GetByIdAsync(int id, CancellationToken ct = default)
        => await _context.Notifications.FindAsync([id], ct);

    public async Task AddAsync(Notification notification, CancellationToken ct = default)
    {
        await _context.Notifications.AddAsync(notification, ct);
        await _context.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Notification notification, CancellationToken ct = default)
    {
        _context.Notifications.Update(notification);
        await _context.SaveChangesAsync(ct);
    }

    public async Task MarkAllAsReadAsync(int userId, CancellationToken ct = default)
    {
        await _context.Notifications
            .Where(n => n.UserId == userId && n.ReadAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.ReadAt, DateTime.UtcNow), ct);
    }
}