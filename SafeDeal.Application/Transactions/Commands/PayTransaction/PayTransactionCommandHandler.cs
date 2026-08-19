using MediatR;
using SafeDeal.Application.Common.Exceptions;
using SafeDeal.Domain.Enums;
using SafeDeal.Domain.Events;
using SafeDeal.Domain.Interfaces.Repositories;

namespace SafeDeal.Application.Transactions.Commands.PayTransaction;

public class PayTransactionCommandHandler : IRequestHandler<PayTransactionCommand>
{
    private readonly ITransactionRepository _transactions;
    private readonly IPublisher _publisher;

    public PayTransactionCommandHandler(ITransactionRepository transactions, IPublisher publisher)
    {
        _transactions = transactions;
        _publisher = publisher;
    }

    public async Task Handle(PayTransactionCommand request, CancellationToken ct)
    {
        var transaction = await _transactions.GetByIdAsync(request.TransactionId, ct)
            ?? throw new NotFoundException("Transaction", request.TransactionId);

        transaction.SetStripePaymentIntent(request.PaymentIntentId);
        transaction.Transition(TransactionStatus.PaymentReceived);
        await _transactions.UpdateAsync(transaction, ct);

        await _publisher.Publish(new TransactionStatusChangedEvent(transaction.Id, TransactionStatus.PaymentReceived), ct);
    }
}
