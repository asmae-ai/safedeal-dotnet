using MediatR;
using Microsoft.Extensions.Logging;
using SafeDeal.Application.Common.Exceptions;
using SafeDeal.Domain.Enums;
using SafeDeal.Domain.Events;
using SafeDeal.Domain.Interfaces.Repositories;

namespace SafeDeal.Application.Transactions.Commands.PayTransaction;

public class PayTransactionCommandHandler : IRequestHandler<PayTransactionCommand>
{
    private readonly ITransactionRepository _transactions;
    private readonly IPublisher _publisher;
    private readonly ILogger<PayTransactionCommandHandler> _logger;

    public PayTransactionCommandHandler(
        ITransactionRepository transactions,
        IPublisher publisher,
        ILogger<PayTransactionCommandHandler> logger)
    {
        _transactions = transactions;
        _publisher = publisher;
        _logger = logger;
    }

    public async Task Handle(PayTransactionCommand request, CancellationToken ct)
    {
        var transaction = await _transactions.GetByIdAsync(request.TransactionId, ct)
            ?? throw new NotFoundException("Transaction", request.TransactionId);

        // Stripe rejoue les webhooks jusqu'à obtenir un 2xx. Un rejeu sur une transaction
        // déjà payée doit être acquitté sans rien refaire, sinon Stripe boucle et les
        // notifications sont dupliquées.
        if (transaction.Status != TransactionStatus.PendingPayment)
        {
            _logger.LogInformation(
                "Stripe webhook replayed for transaction {TransactionId} already in status {Status}; ignored.",
                transaction.Id, transaction.Status);
            return;
        }

        transaction.SetStripeSession(request.SessionId);
        transaction.SetStripePaymentIntent(request.PaymentIntentId);
        transaction.Transition(TransactionStatus.PaymentReceived);
        await _transactions.UpdateAsync(transaction, ct);

        await _publisher.Publish(new TransactionStatusChangedEvent(transaction.Id, TransactionStatus.PaymentReceived), ct);
    }
}
