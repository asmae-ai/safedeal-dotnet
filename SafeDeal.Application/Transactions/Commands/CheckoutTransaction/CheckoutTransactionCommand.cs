using MediatR;

namespace SafeDeal.Application.Transactions.Commands.CheckoutTransaction;

public record CheckoutTransactionCommand(int TransactionId, int UserId) : IRequest<CheckoutResponseDto>;
public record CheckoutResponseDto(string CheckoutUrl, string SessionId);