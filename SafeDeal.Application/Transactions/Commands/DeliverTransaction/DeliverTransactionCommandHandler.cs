using MediatR;
using SafeDeal.Application.Common.Exceptions;
using SafeDeal.Application.Transactions.Commands.CreateTransaction;
using SafeDeal.Application.Transactions.DTOs;
using SafeDeal.Domain.Enums;
using SafeDeal.Domain.Events;
using SafeDeal.Domain.Interfaces.Repositories;

namespace SafeDeal.Application.Transactions.Commands.DeliverTransaction;

public class DeliverTransactionCommandHandler : IRequestHandler<DeliverTransactionCommand, TransactionDto>
{
    private readonly ITransactionRepository _transactions;
    private readonly IUserRepository _users;
    private readonly IPublisher _publisher;

    public DeliverTransactionCommandHandler(ITransactionRepository transactions, IUserRepository users, IPublisher publisher)
    {
        _transactions = transactions;
        _users = users;
        _publisher = publisher;
    }

    public async Task<TransactionDto> Handle(DeliverTransactionCommand request, CancellationToken ct)
    {
        var transaction = await _transactions.GetByIdAsync(request.TransactionId, ct)
            ?? throw new NotFoundException("Transaction", request.TransactionId);

        transaction.Transition(TransactionStatus.Delivered);
        await _transactions.UpdateAsync(transaction, ct);

        await _publisher.Publish(new TransactionStatusChangedEvent(transaction.Id, TransactionStatus.Delivered), ct);

        var vendor = await _users.GetByIdAsync(transaction.VendorId, ct);
        var buyer = transaction.BuyerId.HasValue ? await _users.GetByIdAsync(transaction.BuyerId.Value, ct) : null;
        return CreateTransactionCommandHandler.MapToDto(transaction, vendor!, buyer);
    }
}
