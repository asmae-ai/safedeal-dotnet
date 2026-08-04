using MediatR;

namespace SafeDeal.Application.Disputes.Commands.ResolveDispute;

public record ResolveDisputeCommand(
    int TransactionId,
    string Decision,
    string Note) : IRequest;