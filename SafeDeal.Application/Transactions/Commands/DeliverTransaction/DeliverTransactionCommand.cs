using MediatR;
using SafeDeal.Application.Transactions.DTOs;

namespace SafeDeal.Application.Transactions.Commands.DeliverTransaction;

public record DeliverTransactionCommand(int TransactionId, int BuyerId) : IRequest<TransactionDto>;