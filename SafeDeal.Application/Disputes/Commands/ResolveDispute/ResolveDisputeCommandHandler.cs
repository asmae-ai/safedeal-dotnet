using MediatR;
using SafeDeal.Application.Common.Exceptions;
using SafeDeal.Domain.Enums;
using SafeDeal.Domain.Events;
using SafeDeal.Domain.Interfaces.Repositories;

namespace SafeDeal.Application.Disputes.Commands.ResolveDispute;

public class ResolveDisputeCommandHandler : IRequestHandler<ResolveDisputeCommand>
{
    private readonly IDisputeRepository _disputes;
    private readonly ITransactionRepository _transactions;
    private readonly IPublisher _publisher;

    public ResolveDisputeCommandHandler(IDisputeRepository disputes, ITransactionRepository transactions, IPublisher publisher)
    {
        _disputes = disputes;
        _transactions = transactions;
        _publisher = publisher;
    }

    public async Task Handle(ResolveDisputeCommand request, CancellationToken ct)
    {
        var dispute = await _disputes.GetByIdAsync(request.DisputeId, ct)
            ?? throw new NotFoundException("Dispute", request.DisputeId);

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

        dispute.Resolve(request.Note);
        transaction.Transition(newStatus);

        await _disputes.UpdateAsync(dispute, ct);
        await _transactions.UpdateAsync(transaction, ct);

        await _publisher.Publish(new TransactionStatusChangedEvent(transaction.Id, newStatus), ct);
    }
}
