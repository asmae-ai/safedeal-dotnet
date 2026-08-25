using MediatR;
using Microsoft.Extensions.Logging;
using SafeDeal.Application.Common.Exceptions;
using SafeDeal.Application.Common.Interfaces;
using SafeDeal.Domain.Enums;
using SafeDeal.Domain.Events;
using SafeDeal.Domain.Exceptions;
using SafeDeal.Domain.Interfaces.Repositories;
using SafeDeal.Domain.Interfaces.Services;

namespace SafeDeal.Application.Disputes.Commands.ResolveDispute;

public class ResolveDisputeCommandHandler : IRequestHandler<ResolveDisputeCommand>
{
    private readonly IDisputeRepository _disputes;
    private readonly ITransactionRepository _transactions;
    private readonly IPaymentService _payments;
    private readonly IApplicationDbContext _context;
    private readonly IPublisher _publisher;
    private readonly ILogger<ResolveDisputeCommandHandler> _logger;

    public ResolveDisputeCommandHandler(
        IDisputeRepository disputes,
        ITransactionRepository transactions,
        IPaymentService payments,
        IApplicationDbContext context,
        IPublisher publisher,
        ILogger<ResolveDisputeCommandHandler> logger)
    {
        _disputes = disputes;
        _transactions = transactions;
        _payments = payments;
        _context = context;
        _publisher = publisher;
        _logger = logger;
    }

    public async Task Handle(ResolveDisputeCommand request, CancellationToken ct)
    {
        var dispute = await _disputes.GetByIdAsync(request.DisputeId, ct)
            ?? throw new NotFoundException("Dispute", request.DisputeId);

        if (dispute.Status is DisputeStatus.Resolved or DisputeStatus.Closed)
            throw new BusinessRuleException("This dispute has already been settled.");

        var transaction = await _transactions.GetByIdAsync(dispute.TransactionId, ct)
            ?? throw new NotFoundException("Transaction", dispute.TransactionId);

        var newStatus = request.Decision switch
        {
            "refunded" => TransactionStatus.Refunded,
            "resolved" => TransactionStatus.Resolved,
            _ => throw new ValidationException(new Dictionary<string, string[]>
            {
                ["decision"] = ["Decision must be 'resolved' or 'refunded'."]
            })
        };

        // Trancher en faveur de l'acheteur doit sortir l'argent du séquestre.
        // Le remboursement passe avant l'écriture du statut : si Stripe refuse,
        // le litige reste ouvert plutôt que d'afficher un remboursement fictif.
        if (newStatus == TransactionStatus.Refunded)
        {
            if (string.IsNullOrEmpty(transaction.StripePaymentIntentId))
                throw new BusinessRuleException(
                    "This transaction has no captured payment to refund.");

            try
            {
                await _payments.RefundAsync(transaction.StripePaymentIntentId, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Stripe refund failed for transaction {TransactionId}.", transaction.Id);
                throw new BusinessRuleException(
                    "The refund was refused by the payment provider. The dispute remains open.");
            }
        }

        dispute.Resolve(request.Note);
        transaction.Transition(newStatus, $"Dispute #{dispute.Id} settled as '{request.Decision}'.");

        await _context.ExecuteInTransactionAsync(async () =>
        {
            await _disputes.UpdateAsync(dispute, ct);
            await _transactions.UpdateAsync(transaction, ct);
        }, ct);

        await _publisher.Publish(new TransactionStatusChangedEvent(transaction.Id, newStatus), ct);
    }
}
