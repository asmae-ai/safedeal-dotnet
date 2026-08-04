using SafeDeal.Domain.Common;

namespace SafeDeal.Domain.Events;

public class TransactionCreatedEvent : BaseEvent
{
    public int TransactionId { get; }
    public int VendorId { get; }
    public TransactionCreatedEvent(int transactionId, int vendorId)
    {
        TransactionId = transactionId;
        VendorId = vendorId;
    }
}