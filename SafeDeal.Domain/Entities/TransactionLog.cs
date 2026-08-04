using SafeDeal.Domain.Common;
using SafeDeal.Domain.Enums;

namespace SafeDeal.Domain.Entities;

public class TransactionLog : BaseEntity
{
    public int TransactionId { get; private set; }
    public TransactionStatus Status { get; private set; }
    public string? Note { get; private set; }

    private TransactionLog() { }

    public static TransactionLog Create(int transactionId, TransactionStatus status, string? note = null)
    {
        return new TransactionLog
        {
            TransactionId = transactionId,
            Status = status,
            Note = note
        };
    }
}