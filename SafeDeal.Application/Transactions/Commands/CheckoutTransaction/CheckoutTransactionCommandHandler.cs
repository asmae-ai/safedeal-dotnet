using MediatR;
using SafeDeal.Application.Common.Exceptions;
using SafeDeal.Application.Transactions.Commands.CheckoutTransaction;
using SafeDeal.Domain.Enums;
using SafeDeal.Domain.Exceptions;
using SafeDeal.Domain.Interfaces.Repositories;
using SafeDeal.Domain.Interfaces.Services;

namespace SafeDeal.Application.Transactions.Commands.CheckoutTransaction;

public class CheckoutTransactionCommandHandler : IRequestHandler<CheckoutTransactionCommand, CheckoutResponseDto>
{
    private readonly ITransactionRepository _transactions;
    private readonly IPaymentService _paymentService;

    public CheckoutTransactionCommandHandler(ITransactionRepository transactions, IPaymentService paymentService)
    {
        _transactions = transactions;
        _paymentService = paymentService;
    }

    public async Task<CheckoutResponseDto> Handle(CheckoutTransactionCommand request, CancellationToken ct)
    {
        var transaction = await _transactions.GetByIdAsync(request.TransactionId, ct)
            ?? throw new NotFoundException("Transaction", request.TransactionId);

        if (transaction.BuyerId != request.UserId)
            throw new ForbiddenException("Only the buyer can checkout this transaction.");

        // Une transaction ne peut être payée qu'une seule fois : c'est le statut qui fait foi,
        // pas la présence d'une session Stripe. Un checkout abandonné doit rester rejouable.
        if (transaction.Status != TransactionStatus.PendingPayment)
            throw new BusinessRuleException(
                "This transaction is no longer awaiting payment and cannot be paid again.");

        var (url, sessionId) = await _paymentService.CreateCheckoutSessionAsync(
            transaction.Id,
            transaction.SecureToken,
            transaction.Amount.Amount,
            transaction.Amount.Currency,
            transaction.Title, ct);

        transaction.SetStripeSession(sessionId);
        await _transactions.UpdateAsync(transaction, ct);

        return new CheckoutResponseDto(url, sessionId);
    }
}