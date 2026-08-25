using MediatR;
using SafeDeal.Application.Common.Exceptions;
using SafeDeal.Application.Disputes.DTOs;
using SafeDeal.Domain.Interfaces.Repositories;

namespace SafeDeal.Application.Disputes.Queries.GetDispute;

public class GetDisputeQueryHandler : IRequestHandler<GetDisputeQuery, DisputeDto>
{
    private readonly IDisputeRepository _disputes;
    private readonly ITransactionRepository _transactions;
    private readonly IUserRepository _users;

    public GetDisputeQueryHandler(IDisputeRepository disputes, ITransactionRepository transactions, IUserRepository users)
    {
        _disputes = disputes;
        _transactions = transactions;
        _users = users;
    }

    public async Task<DisputeDto> Handle(GetDisputeQuery request, CancellationToken ct)
    {
        var transaction = await _transactions.GetByIdAsync(request.TransactionId, ct)
            ?? throw new NotFoundException("Transaction", request.TransactionId);

        if (transaction.VendorId != request.UserId && transaction.BuyerId != request.UserId)
            throw new ForbiddenException("You are not authorized to view this dispute.");

        var dispute = await _disputes.GetByTransactionIdAsync(request.TransactionId, ct)
            ?? throw new NotFoundException("Dispute", request.TransactionId);

        var openedBy = await _users.GetByIdAsync(dispute.OpenedByUserId, ct);

        var evidences = dispute.Messages
            .OrderBy(m => m.CreatedAt)
            .Select(m => new EvidenceDto(
                m.AuthorUserId,
                m.Author?.Name ?? "Utilisateur",
                // Le rôle vient de la transaction elle-même, plus d'une alternance arbitraire.
                m.AuthorUserId == transaction.VendorId ? "vendor" : "buyer",
                m.Body,
                m.Files,
                m.CreatedAt.ToString("o")))
            .ToList();

        return new DisputeDto(
            dispute.Id,
            dispute.TransactionId,
            dispute.Category,
            dispute.Description,
            dispute.Status.ToString().ToLower(),
            dispute.CreatedAt.ToString("o"),
            dispute.ResolutionNote,
            new UserOpenedByDto(openedBy!.Id, openedBy.Name, openedBy.Email),
            evidences);
    }
}
