using MediatR;
using Microsoft.EntityFrameworkCore;
using SafeDeal.Application.Common.Interfaces;
using SafeDeal.Application.Common.Models;
using SafeDeal.Application.Transactions.Commands.CreateTransaction;
using SafeDeal.Application.Transactions.DTOs;

namespace SafeDeal.Application.Admin.Queries.GetAllTransactions;

public class GetAllTransactionsQueryHandler : IRequestHandler<GetAllTransactionsQuery, PagedResult<TransactionDto>>
{
    private readonly IApplicationDbContext _context;
    public GetAllTransactionsQueryHandler(IApplicationDbContext context) => _context = context;

    public async Task<PagedResult<TransactionDto>> Handle(GetAllTransactionsQuery request, CancellationToken ct)
    {
        var query = _context.Transactions
            .Include(t => t.Vendor)
            .Include(t => t.Buyer)
            .OrderByDescending(t => t.CreatedAt);

        var total = await query.CountAsync(ct);
        var items = await query
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(ct);

        var lastPage = (int)Math.Ceiling(total / (double)request.PageSize);

        var dtos = items.Select(t => CreateTransactionCommandHandler.MapToDto(t, t.Vendor, t.Buyer));

        return new PagedResult<TransactionDto>(dtos, request.Page, lastPage, total);
    }
}