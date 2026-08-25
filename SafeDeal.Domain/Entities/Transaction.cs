using SafeDeal.Domain.Common;
using SafeDeal.Domain.Enums;
using SafeDeal.Domain.Exceptions;
using SafeDeal.Domain.ValueObjects;

namespace SafeDeal.Domain.Entities;

public class Transaction : BaseEntity, IAuditableEntity
{
    public string Title { get; private set; } = string.Empty;
    public Money Amount { get; private set; } = null!;
    public TransactionStatus Status { get; private set; } = TransactionStatus.PendingPayment;
    public string SecureToken { get; private set; } = string.Empty;
    public int VendorId { get; private set; }
    public int? BuyerId { get; internal set; }
    public string? TrackingNumber { get; private set; }
    public string? Carrier { get; private set; }
    public string? StripeSessionId { get; private set; }
    public string? StripePaymentIntentId { get; private set; }

    public User Vendor { get; private set; } = null!;
    public User? Buyer { get; private set; }
    public ICollection<TransactionLog> Logs { get; private set; } = [];
    public Dispute? Dispute { get; private set; }

    private static readonly Dictionary<TransactionStatus, TransactionStatus[]> _allowedTransitions = new()
    {
        [TransactionStatus.PendingPayment] = [TransactionStatus.PaymentReceived, TransactionStatus.Cancelled],
        // Un acheteur qui a payé et que le vendeur n'expédie jamais doit pouvoir ouvrir un litige.
        [TransactionStatus.PaymentReceived] = [TransactionStatus.InShipping, TransactionStatus.Cancelled, TransactionStatus.Dispute],
        [TransactionStatus.InShipping] = [TransactionStatus.Delivered, TransactionStatus.Cancelled, TransactionStatus.Dispute, TransactionStatus.Refunded],
        [TransactionStatus.Delivered] = [TransactionStatus.Closed, TransactionStatus.Dispute],
        [TransactionStatus.Dispute] = [TransactionStatus.Resolved, TransactionStatus.Refunded],
        [TransactionStatus.Resolved] = [],
        [TransactionStatus.Refunded] = [],
        [TransactionStatus.Closed] = [],
        [TransactionStatus.Cancelled] = []
    };

    private Transaction() { }

    public static Transaction Create(string title, decimal amount, string currency, int vendorId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);

        return new Transaction
        {
            Title = title.Trim(),
            Amount = new Money(amount, currency),
            VendorId = vendorId,
            SecureToken = new ValueObjects.SecureToken().Value
        };
    }

    public void Claim(int buyerId)
    {
        if (BuyerId is not null) throw new BusinessRuleException("Transaction already claimed.");
        if (buyerId == VendorId) throw new BusinessRuleException("Vendor cannot claim their own transaction.");
        BuyerId = buyerId;
        UpdateTimestamp();
    }

    public void Transition(TransactionStatus newStatus, string? note = null)
    {
        if (!_allowedTransitions[Status].Contains(newStatus))
            throw new InvalidTransitionException(Status.ToString(), newStatus.ToString());

        Status = newStatus;
        Logs.Add(TransactionLog.Create(Id, Status, note));
        UpdateTimestamp();
    }

    public void SetStripeSession(string sessionId)
    {
        StripeSessionId = sessionId;
        UpdateTimestamp();
    }

    public void SetStripePaymentIntent(string paymentIntentId)
    {
        StripePaymentIntentId = paymentIntentId;
        UpdateTimestamp();
    }

    public void SetShipping(string trackingNumber, string carrier)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(trackingNumber);
        ArgumentException.ThrowIfNullOrWhiteSpace(carrier);
        TrackingNumber = trackingNumber.Trim();
        Carrier = carrier.Trim();
        UpdateTimestamp();
    }
}

