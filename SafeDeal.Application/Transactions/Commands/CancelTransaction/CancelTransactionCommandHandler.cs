using MediatR;
using SafeDeal.Application.Common.Exceptions;
using SafeDeal.Application.Transactions.Commands.CreateTransaction;
using SafeDeal.Application.Transactions.DTOs;
using SafeDeal.Domain.Enums;
using SafeDeal.Domain.Events;
using SafeDeal.Domain.Interfaces.Repositories;

namespace SafeDeal.Application.Transactions.Commands.CancelTransaction;

public class CancelTransactionCommandHandler : IRequestHandler<CancelTransactionCommand, TransactionDto>
{
    private readonly ITransactionRepository _transactions;
    private readonly IUserRepository _users;
    private readonly IPublisher _publisher;

    public CancelTransactionCommandHandler(ITransactionRepository transactions, IUserRepository users, IPublisher publisher)
    {
        _transactions = transactions;
        _users = users;
        _publisher = publisher;
    }

    public async Task<TransactionDto> Handle(CancelTransactionCommand request, CancellationToken ct)
    {
        var transaction = await _transactions.GetByIdAsync(request.TransactionId, ct)
            ?? throw new NotFoundException("Transaction", request.TransactionId);

        transaction.Transition(TransactionStatus.Cancelled);
        await _transactions.UpdateAsync(transaction, ct);

        await _publisher.Publish(new TransactionStatusChangedEvent(transaction.Id, TransactionStatus.Cancelled), ct);

        var vendor = await _users.GetByIdAsync(transaction.VendorId, ct);
        var buyer = transaction.BuyerId.HasValue ? await _users.GetByIdAsync(transaction.BuyerId.Value, ct) : null;
        return CreateTransactionCommandHandler.MapToDto(transaction, vendor!, buyer);
    }
}
