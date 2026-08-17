using MediatR;

namespace SafeDeal.Application.Disputes.Commands.ResolveDispute;

public record ResolveDisputeCommand(
    int DisputeId,
    string Decision,
    string Note) : IRequest;