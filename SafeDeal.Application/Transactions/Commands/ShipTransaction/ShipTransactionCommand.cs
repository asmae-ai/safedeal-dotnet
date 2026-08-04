using MediatR;
using SafeDeal.Application.Transactions.DTOs;

namespace SafeDeal.Application.Transactions.Commands.ShipTransaction;

public record ShipTransactionCommand(int TransactionId, int VendorId, string TrackingNumber, string Carrier) : IRequest<TransactionDto>;