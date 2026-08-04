using MediatR;
using SafeDeal.Application.Common.Exceptions;
using SafeDeal.Application.Transactions.Commands.CreateTransaction;
using SafeDeal.Application.Transactions.DTOs;
using SafeDeal.Domain.Enums;
using SafeDeal.Domain.Interfaces.Repositories;

namespace SafeDeal.Application.Transactions.Commands.ShipTransaction;

public class ShipTransactionCommandHandler : IRequestHandler<ShipTransactionCommand, TransactionDto>
{
    private readonly ITransactionRepository _transactions;
    private readonly IUserRepository _users;

    public ShipTransactionCommandHandler(ITransactionRepository transactions, IUserRepository users)
    {
        _transactions = transactions;
        _users = users;
    }

    public async Task<TransactionDto> Handle(ShipTransactionCommand request, CancellationToken ct)
    {
        var transaction = await _transactions.GetByIdAsync(request.TransactionId, ct)
            ?? throw new NotFoundException("Transaction", request.TransactionId);

        if (transaction.VendorId != request.VendorId)
            throw new ForbiddenException("Only the vendor can ship this transaction.");

        transaction.SetShipping(request.TrackingNumber, request.Carrier);
        transaction.Transition(TransactionStatus.InShipping);
        await _transactions.UpdateAsync(transaction, ct);

        var vendor = await _users.GetByIdAsync(transaction.VendorId, ct);
        var buyer = transaction.BuyerId.HasValue ? await _users.GetByIdAsync(transaction.BuyerId.Value, ct) : null;

        return CreateTransactionCommandHandler.MapToDto(transaction, vendor!, buyer);
    }
}