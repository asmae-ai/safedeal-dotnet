using MediatR;
using SafeDeal.Application.Common.Models;
using SafeDeal.Application.Transactions.Commands.CreateTransaction;
using SafeDeal.Application.Transactions.DTOs;
using SafeDeal.Domain.Interfaces.Repositories;

namespace SafeDeal.Application.Transactions.Queries.GetTransactions;

public class GetTransactionsQueryHandler : IRequestHandler<GetTransactionsQuery, PagedResult<TransactionDto>>
{
    private readonly ITransactionRepository _transactions;
    private readonly IUserRepository _users;

    public GetTransactionsQueryHandler(ITransactionRepository transactions, IUserRepository users)
    {
        _transactions = transactions;
        _users = users;
    }

    public async Task<PagedResult<TransactionDto>> Handle(GetTransactionsQuery request, CancellationToken ct)
    {
        var (items, total) = await _transactions.GetByUserIdAsync(request.UserId, request.Page, request.PageSize, ct);
        var lastPage = (int)Math.Ceiling(total / (double)request.PageSize);

        var dtos = new List<TransactionDto>();
        foreach (var t in items)
        {
            var vendor = await _users.GetByIdAsync(t.VendorId, ct);
            var buyer = t.BuyerId.HasValue ? await _users.GetByIdAsync(t.BuyerId.Value, ct) : null;
            dtos.Add(CreateTransactionCommandHandler.MapToDto(t, vendor!, buyer));
        }

        return new PagedResult<TransactionDto>(dtos, request.Page, lastPage, total);
    }
}