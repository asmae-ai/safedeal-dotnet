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

        return new DisputeDto(
            dispute.Id,
            dispute.Category,
            dispute.Description,
            dispute.Status.ToString().ToLower(),
            dispute.CreatedAt.ToString("o"),
            new UserOpenedByDto(openedBy!.Id, openedBy.Name, openedBy.Email),
            dispute.EvidenceFiles.Select((f, i) => new EvidenceDto(
                i % 2 == 0 ? "buyer" : "vendor",
                string.Empty,
                [f],
                dispute.CreatedAt.ToString("o"))));
    }
}