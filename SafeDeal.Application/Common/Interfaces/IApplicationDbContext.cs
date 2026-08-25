using Microsoft.EntityFrameworkCore;
using SafeDeal.Domain.Entities;

namespace SafeDeal.Application.Common.Interfaces;

public interface IApplicationDbContext
{
    DbSet<User> Users { get; }
    DbSet<Transaction> Transactions { get; }
    DbSet<TransactionLog> TransactionLogs { get; }
    DbSet<Dispute> Disputes { get; }
    DbSet<DisputeMessage> DisputeMessages { get; }
    DbSet<Notification> Notifications { get; }
    DbSet<IdentityVerification> IdentityVerifications { get; }
    Task<int> SaveChangesAsync(CancellationToken ct = default);

    /// <summary>
    /// Exécute plusieurs écritures dans une seule transaction base de données.
    /// Nécessaire dès qu'une commande touche deux agrégats : les repositories
    /// committent individuellement, ce qui laisserait sinon un état partiel.
    /// </summary>
    Task ExecuteInTransactionAsync(Func<Task> operation, CancellationToken ct = default);
}