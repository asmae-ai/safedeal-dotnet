using MediatR;
using SafeDeal.Application.Transactions.DTOs;

namespace SafeDeal.Application.Disputes.Commands.OpenDispute;

public record OpenDisputeCommand(
    int TransactionId,
    int BuyerId,
    string Category,
    string Description,
    IEnumerable<string> FilePaths) : IRequest<TransactionDto>;