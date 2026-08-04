namespace SafeDeal.Domain.Enums;

public enum TransactionStatus
{
    PendingPayment,
    PaymentReceived,
    InShipping,
    Delivered,
    Closed,
    Cancelled,
    Dispute,
    Resolved,
    Refunded
}