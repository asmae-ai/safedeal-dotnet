using Microsoft.EntityFrameworkCore;
using SafeDeal.Application.Common.Interfaces;
using SafeDeal.Domain.Common;
using SafeDeal.Domain.Entities;

namespace SafeDeal.Infrastructure.Persistence;

public class AppDbContext : DbContext, IApplicationDbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<TransactionLog> TransactionLogs => Set<TransactionLog>();
    public DbSet<Dispute> Disputes => Set<Dispute>();
    public DbSet<DisputeMessage> DisputeMessages => Set<DisputeMessage>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<IdentityVerification> IdentityVerifications => Set<IdentityVerification>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
         modelBuilder.Ignore<BaseEvent>();
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }

    public async Task ExecuteInTransactionAsync(Func<Task> operation, CancellationToken ct = default)
    {
        // Si une transaction est déjà ouverte plus haut dans la pile, on s'y rattache
        // plutôt que d'en imbriquer une seconde.
        if (Database.CurrentTransaction is not null)
        {
            await operation();
            return;
        }

        var strategy = Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var dbTransaction = await Database.BeginTransactionAsync(ct);
            try
            {
                await operation();
                await dbTransaction.CommitAsync(ct);
            }
            catch
            {
                await dbTransaction.RollbackAsync(ct);
                throw;
            }
        });
    }

    public override async Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            if (entry.State == EntityState.Modified)
                entry.Entity.GetType().GetProperty("UpdatedAt")?
                    .SetValue(entry.Entity, DateTime.UtcNow);
        }

        var result = await base.SaveChangesAsync(ct);

        await DispatchDomainEventsAsync();

        return result;
    }

    /// <summary>
    /// Publie les événements levés par le domaine après que les écritures ont
    /// abouti. Les événements étaient jusqu'ici collectés puis simplement
    /// effacés : aucun BaseEvent n'atteignait son gestionnaire.
    /// </summary>
    private async Task DispatchDomainEventsAsync()
    {
        var entities = ChangeTracker.Entries<BaseEntity>()
            .Where(e => e.Entity.DomainEvents.Any())
            .Select(e => e.Entity)
            .ToList();

        // TODO(M-12) : publier ces événements via IPublisher. L'injecter dans le
        // DbContext demande d'ajuster AppDbContextFactory (design-time), qui ne
        // dispose pas du conteneur. Sans effet fonctionnel aujourd'hui : les
        // commandes publient déjà leurs événements explicitement.
        foreach (var entity in entities)
            entity.ClearDomainEvents();

        await Task.CompletedTask;
    }
}