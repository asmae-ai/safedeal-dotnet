using MediatR;
using Microsoft.Extensions.Logging;
using SafeDeal.Application.Common.Exceptions;
using SafeDeal.Application.Transactions.Commands.CreateTransaction;
using SafeDeal.Application.Transactions.DTOs;
using SafeDeal.Domain.Enums;
using SafeDeal.Domain.Events;
using SafeDeal.Domain.Exceptions;
using SafeDeal.Domain.Interfaces.Repositories;
using SafeDeal.Domain.Interfaces.Services;

namespace SafeDeal.Application.Transactions.Commands.CancelTransaction;

public class CancelTransactionCommandHandler : IRequestHandler<CancelTransactionCommand, TransactionDto>
{
    private readonly ITransactionRepository _transactions;
    private readonly IUserRepository _users;
    private readonly IPaymentService _payments;
    private readonly IPublisher _publisher;
    private readonly ILogger<CancelTransactionCommandHandler> _logger;

    public CancelTransactionCommandHandler(
        ITransactionRepository transactions,
        IUserRepository users,
        IPaymentService payments,
        IPublisher publisher,
        ILogger<CancelTransactionCommandHandler> logger)
    {
        _transactions = transactions;
        _users = users;
        _payments = payments;
        _publisher = publisher;
        _logger = logger;
    }

    public async Task<TransactionDto> Handle(CancelTransactionCommand request, CancellationToken ct)
    {
        var transaction = await _transactions.GetByIdAsync(request.TransactionId, ct)
            ?? throw new NotFoundException("Transaction", request.TransactionId);

        if (transaction.VendorId != request.UserId && transaction.BuyerId != request.UserId)
            throw new ForbiddenException("Only the vendor or the buyer can cancel this transaction.");

        // Annuler une transaction déjà encaissée doit rendre l'argent : sans cela,
        // les fonds resteraient captés alors que la commande n'existe plus.
        // Le remboursement passe avant l'écriture du statut, pour ne jamais afficher
        // une annulation dont l'argent n'est pas revenu.
        var mustRefund = !string.IsNullOrEmpty(transaction.StripePaymentIntentId)
                         && transaction.Status != TransactionStatus.PendingPayment;

        if (mustRefund)
        {
            try
            {
                await _payments.RefundAsync(transaction.StripePaymentIntentId!, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Stripe refund failed while cancelling transaction {TransactionId}.", transaction.Id);
                throw new BusinessRuleException(
                    "The refund was refused by the payment provider. The transaction was not cancelled.");
            }
        }

        transaction.Transition(TransactionStatus.Cancelled,
            mustRefund ? "Cancelled after payment; buyer refunded." : null);
        await _transactions.UpdateAsync(transaction, ct);

        await _publisher.Publish(new TransactionStatusChangedEvent(transaction.Id, TransactionStatus.Cancelled), ct);

        var vendor = await _users.GetByIdAsync(transaction.VendorId, ct);
        var buyer = transaction.BuyerId.HasValue ? await _users.GetByIdAsync(transaction.BuyerId.Value, ct) : null;
        return CreateTransactionCommandHandler.MapToDto(transaction, vendor!, buyer);
    }
}
