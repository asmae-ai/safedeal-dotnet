using SafeDeal.Domain.Common;
using SafeDeal.Domain.Enums;

namespace SafeDeal.Domain.Events;

public class TransactionStatusChangedEvent : BaseEvent
{
    public int TransactionId { get; }
    public TransactionStatus NewStatus { get; }
    public int? NotifyUserId { get; }

    public TransactionStatusChangedEvent(int transactionId, TransactionStatus newStatus, int? notifyUserId = null)
    {
        TransactionId = transactionId;
        NewStatus = newStatus;
        NotifyUserId = notifyUserId;
    }
}