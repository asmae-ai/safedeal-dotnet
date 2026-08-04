using MediatR;
using SafeDeal.Application.Common.Exceptions;
using SafeDeal.Application.Transactions.Commands.CreateTransaction;
using SafeDeal.Application.Transactions.DTOs;
using SafeDeal.Domain.Enums;
using SafeDeal.Domain.Interfaces.Repositories;

namespace SafeDeal.Application.Transactions.Commands.DeliverTransaction;

public class DeliverTransactionCommandHandler : IRequestHandler<DeliverTransactionCommand, TransactionDto>
{
    private readonly ITransactionRepository _transactions;
    private readonly IUserRepository _users;

    public DeliverTransactionCommandHandler(ITransactionRepository transactions, IUserRepository users)
    {
        _transactions = transactions;
        _users = users;
    }

    public async Task<TransactionDto> Handle(DeliverTransactionCommand request, CancellationToken ct)
    {
        var transaction = await _transactions.GetByIdAsync(request.TransactionId, ct)
            ?? throw new NotFoundException("Transaction", request.TransactionId);

        if (transaction.BuyerId != request.BuyerId)
            throw new ForbiddenException("Only the buyer can confirm delivery.");

        transaction.Transition(TransactionStatus.Delivered);
        await _transactions.UpdateAsync(transaction, ct);

        var vendor = await _users.GetByIdAsync(transaction.VendorId, ct);
        var buyer = await _users.GetByIdAsync(request.BuyerId, ct);

        return CreateTransactionCommandHandler.MapToDto(transaction, vendor!, buyer);
    }
}