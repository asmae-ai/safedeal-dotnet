using MediatR;
using SafeDeal.Application.Transactions.DTOs;

namespace SafeDeal.Application.Transactions.Commands.CancelTransaction;

public record CancelTransactionCommand(int TransactionId, int UserId) : IRequest<TransactionDto>;