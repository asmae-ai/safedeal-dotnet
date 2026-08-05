using MediatR;

namespace SafeDeal.Application.Transactions.Commands.PayTransaction;

public record PayTransactionCommand(int TransactionId, string SessionId, string PaymentIntentId) : IRequest;