using MediatR;

namespace SafeDeal.Application.Identity.Commands.SubmitVerification;

public record SubmitVerificationCommand(
    int UserId,
    string DocumentType,
    string DocumentFrontPath,
    string SelfiePath) : IRequest;