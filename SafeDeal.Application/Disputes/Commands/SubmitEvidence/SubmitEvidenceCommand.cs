using MediatR;

namespace SafeDeal.Application.Disputes.Commands.SubmitEvidence;

public record SubmitEvidenceCommand(
    int TransactionId,
    int UserId,
    string Description,
    IEnumerable<string> FilePaths) : IRequest;