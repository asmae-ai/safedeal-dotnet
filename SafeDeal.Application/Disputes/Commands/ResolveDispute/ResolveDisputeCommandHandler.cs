using MediatR;
using SafeDeal.Application.Common.Exceptions;
using SafeDeal.Domain.Enums;
using SafeDeal.Domain.Interfaces.Repositories;

namespace SafeDeal.Application.Disputes.Commands.ResolveDispute;

public class ResolveDisputeCommandHandler : IRequestHandler<ResolveDisputeCommand>
{
    private readonly IDisputeRepository _disputes;
    private readonly ITransactionRepository _transactions;

    public ResolveDisputeCommandHandler(IDisputeRepository disputes, ITransactionRepository transactions)
    {
        _disputes = disputes;
        _transactions = transactions;
    }

    public async Task Handle(ResolveDisputeCommand request, CancellationToken ct)
    {
        var transaction = await _transactions.GetByIdAsync(request.TransactionId, ct)
            ?? throw new NotFoundException("Transaction", request.TransactionId);

        var dispute = await _disputes.GetByTransactionIdAsync(request.TransactionId, ct)
            ?? throw new NotFoundException("Dispute", request.TransactionId);

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
    }
}