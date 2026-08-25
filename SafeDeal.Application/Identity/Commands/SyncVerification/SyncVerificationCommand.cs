using MediatR;

namespace SafeDeal.Application.Identity.Commands.SyncVerification;

/// <param name="Answer">"GREEN" (acceptée) ou "RED" (refusée), tel que renvoyé par Sumsub.</param>
public record SyncVerificationCommand(
    string ApplicantId,
    string? ExternalUserId,
    string Answer,
    string? Reason) : IRequest<bool>;
