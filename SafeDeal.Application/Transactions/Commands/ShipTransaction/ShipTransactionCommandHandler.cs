using MediatR;
using SafeDeal.Application.Common.Exceptions;
using SafeDeal.Application.Transactions.Commands.CreateTransaction;
using SafeDeal.Application.Transactions.DTOs;
using SafeDeal.Domain.Enums;
using SafeDeal.Domain.Events;
using SafeDeal.Domain.Interfaces.Repositories;

namespace SafeDeal.Application.Transactions.Commands.ShipTransaction;

public class ShipTransactionCommandHandler : IRequestHandler<ShipTransactionCommand, TransactionDto>
{
    private readonly ITransactionRepository _transactions;
    private readonly IUserRepository _users;
    private readonly IPublisher _publisher;

    public ShipTransactionCommandHandler(ITransactionRepository transactions, IUserRepository users, IPublisher publisher)
    {
        _transactions = transactions;
        _users = users;
        _publisher = publisher;
    }

    public async Task<TransactionDto> Handle(ShipTransactionCommand request, CancellationToken ct)
    {
        var transaction = await _transactions.GetByIdAsync(request.TransactionId, ct)
            ?? throw new NotFoundException("Transaction", request.TransactionId);

        if (transaction.VendorId != request.VendorId)
            throw new ForbiddenException();

        transaction.SetShipping(request.TrackingNumber, request.Carrier);
        transaction.Transition(TransactionStatus.InShipping);
        await _transactions.UpdateAsync(transaction, ct);

        await _publisher.Publish(new TransactionStatusChangedEvent(transaction.Id, TransactionStatus.InShipping), ct);

        var vendor = await _users.GetByIdAsync(transaction.VendorId, ct);
        var buyer = transaction.BuyerId.HasValue ? await _users.GetByIdAsync(transaction.BuyerId.Value, ct) : null;
        return CreateTransactionCommandHandler.MapToDto(transaction, vendor!, buyer);
    }
}
