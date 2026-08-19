using MediatR;
using SafeDeal.Domain.Entities;
using SafeDeal.Domain.Events;
using SafeDeal.Domain.Interfaces.Repositories;

namespace SafeDeal.Application.Notifications.EventHandlers;

public class DisputeOpenedNotificationHandler : INotificationHandler<DisputeOpenedEvent>
{
    private readonly INotificationRepository _notifications;
    private readonly ITransactionRepository _transactions;

    public DisputeOpenedNotificationHandler(
        INotificationRepository notifications,
        ITransactionRepository transactions)
    {
        _notifications = notifications;
        _transactions = transactions;
    }

    public async Task Handle(DisputeOpenedEvent evt, CancellationToken ct)
    {
        var transaction = await _transactions.GetByIdAsync(evt.TransactionId, ct);
        if (transaction is null) return;

        var notification = Notification.Create(transaction.VendorId,
            $"Un litige a été ouvert pour la transaction '{transaction.Title}'.");
        await _notifications.AddAsync(notification, ct);
    }
}
