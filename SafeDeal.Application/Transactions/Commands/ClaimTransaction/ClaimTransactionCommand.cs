using MediatR;
using SafeDeal.Application.Transactions.DTOs;

namespace SafeDeal.Application.Transactions.Commands.ClaimTransaction;

public record ClaimTransactionCommand(string Token, int BuyerId) : IRequest<TransactionDto>;