using MediatR;
using SafeDeal.Application.Transactions.DTOs;

namespace SafeDeal.Application.Transactions.Queries.GetTransactionByToken;

public record GetTransactionByTokenQuery(string Token) : IRequest<TransactionDto>;