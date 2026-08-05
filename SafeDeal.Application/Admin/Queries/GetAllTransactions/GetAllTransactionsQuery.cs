using MediatR;
using SafeDeal.Application.Common.Models;
using SafeDeal.Application.Transactions.DTOs;

namespace SafeDeal.Application.Admin.Queries.GetAllTransactions;

public record GetAllTransactionsQuery(int Page = 1, int PageSize = 20) : IRequest<PagedResult<TransactionDto>>;