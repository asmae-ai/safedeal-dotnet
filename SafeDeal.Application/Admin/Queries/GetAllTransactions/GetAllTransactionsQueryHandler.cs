using MediatR;
using Microsoft.EntityFrameworkCore;
using SafeDeal.Application.Common.Interfaces;
using SafeDeal.Application.Common.Models;
using SafeDeal.Application.Transactions.Commands.CreateTransaction;
using SafeDeal.Application.Transactions.DTOs;
using SafeDeal.Domain.Enums;

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
            .AsQueryable();

        // Recherche et filtre appliques en base, pas sur la seule page chargee.
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim().ToLower();
            query = query.Where(t => t.Title.ToLower().Contains(term)
                                  || t.Vendor.Name.ToLower().Contains(term)
                                  || (t.Buyer != null && t.Buyer.Name.ToLower().Contains(term)));
        }

        if (!string.IsNullOrWhiteSpace(request.Status)
            && Enum.TryParse<TransactionStatus>(request.Status.Replace("_", ""), ignoreCase: true, out var status))
        {
            query = query.Where(t => t.Status == status);
        }

        var total = await query.CountAsync(ct);
        var lastPage = total == 0 ? 1 : (int)Math.Ceiling(total / (double)request.PageSize);

        var items = await query
            .OrderByDescending(t => t.CreatedAt)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(ct);

        var dtos = items.Select(t => CreateTransactionCommandHandler.MapToDto(t, t.Vendor, t.Buyer)).ToList();

        return new PagedResult<TransactionDto>(dtos, request.Page, lastPage, total);
    }
}
