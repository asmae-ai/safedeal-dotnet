using MediatR;
using SafeDeal.Domain.Entities;
using SafeDeal.Domain.Enums;
using SafeDeal.Domain.Events;
using SafeDeal.Domain.Interfaces.Repositories;

namespace SafeDeal.Application.Notifications.EventHandlers;

public class TransactionStatusChangedNotificationHandler : INotificationHandler<TransactionStatusChangedEvent>
{
    private readonly INotificationRepository _notifications;
    private readonly ITransactionRepository _transactions;

    public TransactionStatusChangedNotificationHandler(
        INotificationRepository notifications,
        ITransactionRepository transactions)
    {
        _notifications = notifications;
        _transactions = transactions;
    }

    public async Task Handle(TransactionStatusChangedEvent evt, CancellationToken ct)
    {
        var transaction = await _transactions.GetByIdAsync(evt.TransactionId, ct);
        if (transaction is null) return;

        var messages = new List<(int UserId, string Message)>();

        switch (evt.NewStatus)
        {
            case TransactionStatus.PaymentReceived:
                messages.Add((transaction.VendorId, $"Paiement reçu pour la transaction '{transaction.Title}'. Vous pouvez expédier la commande."));
                if (transaction.BuyerId.HasValue)
                    messages.Add((transaction.BuyerId.Value, $"Votre paiement pour '{transaction.Title}' a été reçu et sécurisé."));
                break;

            case TransactionStatus.InShipping:
                if (transaction.BuyerId.HasValue)
                    messages.Add((transaction.BuyerId.Value, $"Votre commande '{transaction.Title}' a été expédiée."));
                break;

            case TransactionStatus.Delivered:
                messages.Add((transaction.VendorId, $"La commande '{transaction.Title}' a été marquée comme livrée."));
                break;

            case TransactionStatus.Closed:
                messages.Add((transaction.VendorId, $"La transaction '{transaction.Title}' est terminée. Les fonds ont été libérés."));
                if (transaction.BuyerId.HasValue)
                    messages.Add((transaction.BuyerId.Value, $"La transaction '{transaction.Title}' est terminée."));
                break;

            case TransactionStatus.Cancelled:
                messages.Add((transaction.VendorId, $"La transaction '{transaction.Title}' a été annulée."));
                if (transaction.BuyerId.HasValue)
                    messages.Add((transaction.BuyerId.Value, $"La transaction '{transaction.Title}' a été annulée."));
                break;

            case TransactionStatus.Refunded:
                if (transaction.BuyerId.HasValue)
                    messages.Add((transaction.BuyerId.Value, $"Vous avez été remboursé pour la transaction '{transaction.Title}'."));
                messages.Add((transaction.VendorId, $"La transaction '{transaction.Title}' a été remboursée à l acheteur."));
                break;

            case TransactionStatus.Resolved:
                if (transaction.BuyerId.HasValue)
                    messages.Add((transaction.BuyerId.Value, $"Le litige pour '{transaction.Title}' a été résolu."));
                messages.Add((transaction.VendorId, $"Le litige pour '{transaction.Title}' a été résolu."));
                break;
        }

        foreach (var (userId, message) in messages)
        {
            var notification = Notification.Create(userId, message);
            await _notifications.AddAsync(notification, ct);
        }
    }
}
