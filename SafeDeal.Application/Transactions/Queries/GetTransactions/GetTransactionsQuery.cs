using MediatR;
using SafeDeal.Application.Common.Models;
using SafeDeal.Application.Transactions.DTOs;

namespace SafeDeal.Application.Transactions.Queries.GetTransactions;

public record GetTransactionsQuery(int UserId, int Page = 1, int PageSize = 15) : IRequest<PagedResult<TransactionDto>>;