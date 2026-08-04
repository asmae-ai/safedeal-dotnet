using MediatR;
using SafeDeal.Application.Common.Exceptions;
using SafeDeal.Application.Transactions.Commands.CreateTransaction;
using SafeDeal.Application.Transactions.DTOs;
using SafeDeal.Domain.Interfaces.Repositories;

namespace SafeDeal.Application.Transactions.Queries.GetTransactionByToken;

public class GetTransactionByTokenQueryHandler : IRequestHandler<GetTransactionByTokenQuery, TransactionDto>
{
    private readonly ITransactionRepository _transactions;
    private readonly IUserRepository _users;

    public GetTransactionByTokenQueryHandler(ITransactionRepository transactions, IUserRepository users)
    {
        _transactions = transactions;
        _users = users;
    }

    public async Task<TransactionDto> Handle(GetTransactionByTokenQuery request, CancellationToken ct)
    {
        var transaction = await _transactions.GetByTokenAsync(request.Token, ct)
            ?? throw new NotFoundException("Transaction", request.Token);

        var vendor = await _users.GetByIdAsync(transaction.VendorId, ct);
        var buyer = transaction.BuyerId.HasValue ? await _users.GetByIdAsync(transaction.BuyerId.Value, ct) : null;

        return CreateTransactionCommandHandler.MapToDto(transaction, vendor!, buyer);
    }
}