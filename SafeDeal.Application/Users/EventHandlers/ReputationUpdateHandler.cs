using MediatR;
using SafeDeal.Domain.Enums;
using SafeDeal.Domain.Events;
using SafeDeal.Domain.Interfaces.Repositories;

namespace SafeDeal.Application.Users.EventHandlers;

/// <summary>
/// Fait vivre le score de réputation, jusqu'ici affiché partout et jamais alimenté.
/// Une transaction menée à son terme le fait monter ; un litige tranché contre
/// une partie le fait baisser.
/// </summary>
public class ReputationUpdateHandler : INotificationHandler<TransactionStatusChangedEvent>
{
    private readonly ITransactionRepository _transactions;
    private readonly IUserRepository _users;

    public ReputationUpdateHandler(ITransactionRepository transactions, IUserRepository users)
    {
        _transactions = transactions;
        _users = users;
    }

    public async Task Handle(TransactionStatusChangedEvent evt, CancellationToken ct)
    {
        if (evt.NewStatus is not (TransactionStatus.Closed
                               or TransactionStatus.Resolved
                               or TransactionStatus.Refunded))
            return;

        var transaction = await _transactions.GetByIdAsync(evt.TransactionId, ct);
        if (transaction is null) return;

        var vendor = await _users.GetByIdAsync(transaction.VendorId, ct);
        if (vendor is null) return;

        switch (evt.NewStatus)
        {
            // La transaction s'est bien terminée : les deux parties y gagnent.
            case TransactionStatus.Closed:
            case TransactionStatus.Resolved:
                vendor.RegisterSuccessfulTransaction();
                await _users.UpdateAsync(vendor, ct);

                if (transaction.BuyerId.HasValue)
                {
                    var buyer = await _users.GetByIdAsync(transaction.BuyerId.Value, ct);
                    if (buyer is not null)
                    {
                        buyer.RegisterSuccessfulTransaction();
                        await _users.UpdateAsync(buyer, ct);
                    }
                }
                break;

            // Remboursement : le litige a été tranché contre le vendeur.
            case TransactionStatus.Refunded:
                vendor.RegisterFailedTransaction();
                await _users.UpdateAsync(vendor, ct);
                break;
        }
    }
}
