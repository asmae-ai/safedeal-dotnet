using MediatR;
using SafeDeal.Application.Common.Exceptions;
using SafeDeal.Application.Transactions.Commands.CreateTransaction;
using SafeDeal.Application.Transactions.DTOs;
using SafeDeal.Domain.Entities;
using SafeDeal.Domain.Enums;
using SafeDeal.Domain.Interfaces.Repositories;

namespace SafeDeal.Application.Disputes.Commands.OpenDispute;

public class OpenDisputeCommandHandler : IRequestHandler<OpenDisputeCommand, TransactionDto>
{
    private readonly ITransactionRepository _transactions;
    private readonly IDisputeRepository _disputes;
    private readonly IUserRepository _users;

    public OpenDisputeCommandHandler(
        ITransactionRepository transactions,
        IDisputeRepository disputes,
        IUserRepository users)
    {
        _transactions = transactions;
        _disputes = disputes;
        _users = users;
    }

    public async Task<TransactionDto> Handle(OpenDisputeCommand request, CancellationToken ct)
    {
        var transaction = await _transactions.GetByIdAsync(request.TransactionId, ct)
            ?? throw new NotFoundException("Transaction", request.TransactionId);

        if (transaction.BuyerId != request.BuyerId)
            throw new ForbiddenException("Only the buyer can open a dispute.");

        var existing = await _disputes.GetByTransactionIdAsync(request.TransactionId, ct);
        if (existing is not null)
            throw new ValidationException(new Dictionary<string, string[]>
            {
                ["dispute"] = ["A dispute already exists for this transaction."]
            });

        var dispute = Dispute.Create(request.TransactionId, request.BuyerId, request.Category, request.Description);
        foreach (var file in request.FilePaths)
            dispute.AddEvidence(file);

        await _disputes.AddAsync(dispute, ct);

        transaction.Transition(TransactionStatus.Dispute);
        await _transactions.UpdateAsync(transaction, ct);

        var vendor = await _users.GetByIdAsync(transaction.VendorId, ct);
        var buyer = await _users.GetByIdAsync(request.BuyerId, ct);

        return CreateTransactionCommandHandler.MapToDto(transaction, vendor!, buyer!);
    }
}