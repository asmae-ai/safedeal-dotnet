using SafeDeal.Domain.Common;

namespace SafeDeal.Domain.Events;

public class DisputeOpenedEvent : BaseEvent
{
    public int TransactionId { get; }
    public int DisputeId { get; }
    public DisputeOpenedEvent(int transactionId, int disputeId)
    {
        TransactionId = transactionId;
        DisputeId = disputeId;
    }
}