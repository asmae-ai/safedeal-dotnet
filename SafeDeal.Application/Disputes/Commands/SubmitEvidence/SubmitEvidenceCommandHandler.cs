using MediatR;
using SafeDeal.Application.Common.Exceptions;
using SafeDeal.Domain.Interfaces.Repositories;

namespace SafeDeal.Application.Disputes.Commands.SubmitEvidence;

public class SubmitEvidenceCommandHandler : IRequestHandler<SubmitEvidenceCommand>
{
    private readonly IDisputeRepository _disputes;
    private readonly ITransactionRepository _transactions;

    public SubmitEvidenceCommandHandler(IDisputeRepository disputes, ITransactionRepository transactions)
    {
        _disputes = disputes;
        _transactions = transactions;
    }

    public async Task Handle(SubmitEvidenceCommand request, CancellationToken ct)
    {
        var transaction = await _transactions.GetByIdAsync(request.TransactionId, ct)
            ?? throw new NotFoundException("Transaction", request.TransactionId);

        if (transaction.VendorId != request.UserId && transaction.BuyerId != request.UserId)
            throw new ForbiddenException("You are not authorized to submit evidence.");

        var dispute = await _disputes.GetByTransactionIdAsync(request.TransactionId, ct)
            ?? throw new NotFoundException("Dispute", request.TransactionId);

        // Le texte de la réponse est la pièce maîtresse : il est stocké avec son auteur,
        // les fichiers ne sont que des pièces jointes.
        dispute.AddMessage(request.UserId, request.Description, request.FilePaths);

        await _disputes.UpdateAsync(dispute, ct);
    }
}
