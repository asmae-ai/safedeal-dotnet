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

        var messages = new List<(int UserId, string Message, NotificationType Type)>();

        switch (evt.NewStatus)
        {
            case TransactionStatus.PaymentReceived:
                messages.Add((transaction.VendorId, $"Paiement reçu pour la transaction « {transaction.Title} ». Vous pouvez expédier la commande.", NotificationType.Payment));
                if (transaction.BuyerId.HasValue)
                    messages.Add((transaction.BuyerId.Value, $"Votre paiement pour « {transaction.Title} » a été reçu et sécurisé.", NotificationType.Payment));
                break;

            case TransactionStatus.InShipping:
                if (transaction.BuyerId.HasValue)
                    messages.Add((transaction.BuyerId.Value, $"Votre commande « {transaction.Title} » a été expédiée.", NotificationType.Shipping));
                break;

            case TransactionStatus.Delivered:
                messages.Add((transaction.VendorId, $"La commande « {transaction.Title} » a été marquée comme livrée.", NotificationType.Shipping));
                break;

            case TransactionStatus.Closed:
                messages.Add((transaction.VendorId, $"La transaction « {transaction.Title} » est terminée. Les fonds ont été libérés.", NotificationType.Payment));
                if (transaction.BuyerId.HasValue)
                    messages.Add((transaction.BuyerId.Value, $"La transaction « {transaction.Title} » est terminée.", NotificationType.Transaction));
                break;

            case TransactionStatus.Cancelled:
                messages.Add((transaction.VendorId, $"La transaction « {transaction.Title} » a été annulée.", NotificationType.Transaction));
                if (transaction.BuyerId.HasValue)
                    messages.Add((transaction.BuyerId.Value, $"La transaction « {transaction.Title} » a été annulée.", NotificationType.Transaction));
                break;

            // Le passage en litige est notifié par DisputeOpenedNotificationHandler,
            // qui connaît l'auteur de la réclamation. Pas de doublon ici.
            case TransactionStatus.Dispute:
                break;

            case TransactionStatus.Refunded:
                if (transaction.BuyerId.HasValue)
                    messages.Add((transaction.BuyerId.Value, $"Vous avez été remboursé pour la transaction « {transaction.Title} ».", NotificationType.Payment));
                messages.Add((transaction.VendorId, $"La transaction « {transaction.Title} » a été remboursée à l'acheteur.", NotificationType.Payment));
                break;

            case TransactionStatus.Resolved:
                if (transaction.BuyerId.HasValue)
                    messages.Add((transaction.BuyerId.Value, $"Le litige sur « {transaction.Title} » a été résolu en faveur du vendeur.", NotificationType.Dispute));
                messages.Add((transaction.VendorId, $"Le litige sur « {transaction.Title} » a été résolu en votre faveur. Les fonds vous sont acquis.", NotificationType.Dispute));
                break;
        }

        foreach (var (userId, message, type) in messages)
        {
            var notification = Notification.Create(userId, message, type, transaction.Id);
            await _notifications.AddAsync(notification, ct);
        }
    }
}
