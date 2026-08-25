using MediatR;
using SafeDeal.Domain.Entities;
using SafeDeal.Domain.Enums;
using SafeDeal.Domain.Events;
using SafeDeal.Domain.Interfaces.Repositories;

namespace SafeDeal.Application.Notifications.EventHandlers;

public class DisputeOpenedNotificationHandler : INotificationHandler<DisputeOpenedEvent>
{
    private readonly INotificationRepository _notifications;
    private readonly ITransactionRepository _transactions;
    private readonly IDisputeRepository _disputes;

    public DisputeOpenedNotificationHandler(
        INotificationRepository notifications,
        ITransactionRepository transactions,
        IDisputeRepository disputes)
    {
        _notifications = notifications;
        _transactions = transactions;
        _disputes = disputes;
    }

    public async Task Handle(DisputeOpenedEvent evt, CancellationToken ct)
    {
        var transaction = await _transactions.GetByIdAsync(evt.TransactionId, ct);
        if (transaction is null) return;

        var dispute = await _disputes.GetByIdAsync(evt.DisputeId, ct);
        var openedByVendor = dispute is not null && dispute.OpenedByUserId == transaction.VendorId;

        // Chaque partie reçoit le message qui correspond à son rôle : celle qui a
        // ouvert le litige reçoit un accusé, l'autre une demande de réponse.
        var author = openedByVendor ? transaction.VendorId : transaction.BuyerId;
        var counterparty = openedByVendor ? transaction.BuyerId : transaction.VendorId;

        if (author.HasValue)
            await _notifications.AddAsync(Notification.Create(author.Value,
                $"Votre litige sur « {transaction.Title} » a bien été enregistré. Les fonds restent bloqués jusqu'à la décision.",
                NotificationType.Dispute, transaction.Id), ct);

        if (counterparty.HasValue)
            await _notifications.AddAsync(Notification.Create(counterparty.Value,
                $"Un litige a été ouvert sur la transaction « {transaction.Title} ». Répondez pour faire valoir votre version.",
                NotificationType.Dispute, transaction.Id), ct);
    }
}
