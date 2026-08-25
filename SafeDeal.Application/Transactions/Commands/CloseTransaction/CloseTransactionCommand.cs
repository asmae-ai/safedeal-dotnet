using MediatR;
using SafeDeal.Application.Transactions.DTOs;

namespace SafeDeal.Application.Transactions.Commands.CloseTransaction;

// C'est l'acheteur qui clôture en confirmant la réception : le nom du paramètre le reflète.
public record CloseTransactionCommand(int TransactionId, int UserId) : IRequest<TransactionDto>;