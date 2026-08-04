using MediatR;
using SafeDeal.Application.Common.Exceptions;
using SafeDeal.Application.Transactions.DTOs;
using SafeDeal.Domain.Interfaces.Repositories;
using SafeDeal.Application.Transactions.Commands.CreateTransaction;
namespace SafeDeal.Application.Transactions.Commands.ClaimTransaction;

public class ClaimTransactionCommandHandler : IRequestHandler<ClaimTransactionCommand, TransactionDto>
{
    private readonly ITransactionRepository _transactions;
    private readonly IUserRepository _users;

    public ClaimTransactionCommandHandler(ITransactionRepository transactions, IUserRepository users)
    {
        _transactions = transactions;
        _users = users;
    }

    public async Task<TransactionDto> Handle(ClaimTransactionCommand request, CancellationToken ct)
    {
        var transaction = await _transactions.GetByTokenAsync(request.Token, ct)
            ?? throw new NotFoundException("Transaction", request.Token);

        var buyer = await _users.GetByIdAsync(request.BuyerId, ct)
            ?? throw new NotFoundException("User", request.BuyerId);

        transaction.Claim(request.BuyerId);
        await _transactions.UpdateAsync(transaction, ct);

        var vendor = await _users.GetByIdAsync(transaction.VendorId, ct)!;
        return CreateTransactionCommandHandler.MapToDto(transaction, vendor!, buyer);
    }
}