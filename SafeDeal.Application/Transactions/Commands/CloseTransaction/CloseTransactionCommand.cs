using MediatR;
using SafeDeal.Application.Transactions.DTOs;

namespace SafeDeal.Application.Transactions.Commands.CloseTransaction;

public record CloseTransactionCommand(int TransactionId, int VendorId) : IRequest<TransactionDto>;