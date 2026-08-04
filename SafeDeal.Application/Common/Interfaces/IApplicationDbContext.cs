using Microsoft.EntityFrameworkCore;
using SafeDeal.Domain.Entities;

namespace SafeDeal.Application.Common.Interfaces;

public interface IApplicationDbContext
{
    DbSet<User> Users { get; }
    DbSet<Transaction> Transactions { get; }
    DbSet<TransactionLog> TransactionLogs { get; }
    DbSet<Dispute> Disputes { get; }
    DbSet<Notification> Notifications { get; }
    DbSet<IdentityVerification> IdentityVerifications { get; }
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}