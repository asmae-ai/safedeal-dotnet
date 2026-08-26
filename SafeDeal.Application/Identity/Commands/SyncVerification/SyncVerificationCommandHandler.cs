using MediatR;
using Microsoft.EntityFrameworkCore;
using SafeDeal.Application.Common.Caching;
using Microsoft.Extensions.Logging;
using SafeDeal.Application.Common.Interfaces;
using SafeDeal.Domain.Enums;
using SafeDeal.Domain.Interfaces.Repositories;

namespace SafeDeal.Application.Identity.Commands.SyncVerification;

/// <summary>
/// Applique la décision d'un prestataire de vérification d'identité.
/// Jusqu'ici Sumsub créait un dossier qui n'était jamais relu : toute décision
/// devait être reprise à la main dans l'écran admin.
/// </summary>
public class SyncVerificationCommandHandler : IRequestHandler<SyncVerificationCommand, bool>
{
    private readonly IApplicationDbContext _context;
    private readonly IIdentityVerificationRepository _verifications;
    private readonly IUserRepository _users;
    private readonly ICacheService _cache;
    private readonly ILogger<SyncVerificationCommandHandler> _logger;

    public SyncVerificationCommandHandler(
        IApplicationDbContext context,
        IIdentityVerificationRepository verifications,
        IUserRepository users,
        ICacheService cache,
        ILogger<SyncVerificationCommandHandler> logger)
    {
        _context = context;
        _verifications = verifications;
        _users = users;
        _cache = cache;
        _logger = logger;
    }

    public async Task<bool> Handle(SyncVerificationCommand request, CancellationToken ct)
    {
        // Le dossier est retrouvé par son identifiant Sumsub ; l'identifiant
        // externe sert de repli si le dossier a été cree avant son stockage.
        var verification = await _context.IdentityVerifications
            .FirstOrDefaultAsync(v => v.SumsubApplicantId == request.ApplicantId, ct);

        if (verification is null && int.TryParse(request.ExternalUserId, out var externalId))
            verification = await _verifications.GetByUserIdAsync(externalId, ct);

        if (verification is null)
        {
            _logger.LogWarning("Sumsub decision for unknown applicant {ApplicantId}.", request.ApplicantId);
            return false;
        }

        // Une décision déjà appliquée ne doit pas être rejouée : Sumsub réémet
        // ses notifications tant qu'elles ne sont pas acquittées.
        if (verification.Status is IdentityStatus.Approved or IdentityStatus.Rejected)
            return true;

        var user = await _users.GetByIdAsync(verification.UserId, ct);
        if (user is null) return false;

        if (request.Answer.Equals("GREEN", StringComparison.OrdinalIgnoreCase))
        {
            verification.Approve();
            user.UpdateIdentityStatus(IdentityStatus.Approved);
        }
        else
        {
            var reason = string.IsNullOrWhiteSpace(request.Reason)
                ? "Vérification refusée par le prestataire d'identité."
                : request.Reason;
            verification.Reject(reason);
            user.UpdateIdentityStatus(IdentityStatus.Rejected);
        }

        await _verifications.UpdateAsync(verification, ct);
        await _users.UpdateAsync(user, ct);

        // Meme effet qu'une decision prise a la main dans l'ecran admin.
        await _cache.InvalidateAsync(CacheScopes.User(verification.UserId), ct);
        await _cache.InvalidateAsync(CacheScopes.Admin, ct);

        _logger.LogInformation(
            "Sumsub decision {Answer} applied to user {UserId}.", request.Answer, verification.UserId);

        return true;
    }
}
