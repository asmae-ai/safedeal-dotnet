using MediatR;
using SafeDeal.Application.Common.Caching;
using SafeDeal.Domain.Events;
using SafeDeal.Domain.Interfaces.Repositories;

namespace SafeDeal.Application.Dashboard.EventHandlers;

/// <summary>
/// Périme les tableaux de bord dès qu'une transaction change d'état.
///
/// Le passage par les événements du domaine évite de disperser des appels au
/// cache dans les handlers de paiement, d'expédition ou de litige : ceux-ci
/// annoncent déjà ce qu'ils ont fait, il suffit de les écouter. Les deux
/// parties sont périmées séparément — un incident entre deux comptes n'a aucune
/// raison de vider le cache de toute la plateforme.
/// </summary>
public class DashboardCacheInvalidationHandler :
    INotificationHandler<TransactionStatusChangedEvent>,
    INotificationHandler<DisputeOpenedEvent>
{
    private readonly ICacheService _cache;
    private readonly ITransactionRepository _transactions;

    public DashboardCacheInvalidationHandler(ICacheService cache, ITransactionRepository transactions)
    {
        _cache = cache;
        _transactions = transactions;
    }

    public Task Handle(TransactionStatusChangedEvent notification, CancellationToken ct)
        => InvalidateForTransactionAsync(notification.TransactionId, ct);

    public Task Handle(DisputeOpenedEvent notification, CancellationToken ct)
        => InvalidateForTransactionAsync(notification.TransactionId, ct);

    private async Task InvalidateForTransactionAsync(int transactionId, CancellationToken ct)
    {
        var transaction = await _transactions.GetByIdAsync(transactionId, ct);
        if (transaction is null) return;

        await _cache.InvalidateAsync(CacheScopes.User(transaction.VendorId), ct);

        if (transaction.BuyerId is int buyerId)
            await _cache.InvalidateAsync(CacheScopes.User(buyerId), ct);

        // Les compteurs de plateforme agrègent toutes les transactions.
        await _cache.InvalidateAsync(CacheScopes.Admin, ct);
    }
}
