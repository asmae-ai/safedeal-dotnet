using MediatR;
using SafeDeal.Application.Transactions.DTOs;

namespace SafeDeal.Application.Transactions.Commands.CreateTransaction;

public record CreateTransactionCommand(
    int VendorId,
    string Title,
    decimal Amount,
    string Currency) : IRequest<TransactionDto>;