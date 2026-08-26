using MediatR;
using SafeDeal.Application.Common.Caching;
using SafeDeal.Application.Common.Exceptions;
using SafeDeal.Domain.Interfaces.Repositories;

namespace SafeDeal.Application.Admin.Commands.RejectIdentity;

public class RejectIdentityCommandHandler : IRequestHandler<RejectIdentityCommand>
{
    private readonly IIdentityVerificationRepository _verifications;
    private readonly IUserRepository _users;
    private readonly ICacheService _cache;

    public RejectIdentityCommandHandler(
        IIdentityVerificationRepository verifications,
        IUserRepository users,
        ICacheService cache)
    {
        _verifications = verifications;
        _users = users;
        _cache = cache;
    }

    public async Task Handle(RejectIdentityCommand request, CancellationToken ct)
    {
        var verification = await _verifications.GetByUserIdAsync(request.UserId, ct)
            ?? throw new NotFoundException("IdentityVerification", request.UserId);

        var user = await _users.GetByIdAsync(request.UserId, ct)
            ?? throw new NotFoundException("User", request.UserId);

        verification.Reject(request.Reason);
        user.UpdateIdentityStatus(Domain.Enums.IdentityStatus.Rejected);

        await _verifications.UpdateAsync(verification, ct);
        await _users.UpdateAsync(user, ct);

        // La file d'attente des verifications et le statut vu par l'interesse
        // changent tous deux : les deux portees sont perimees.
        await _cache.InvalidateAsync(CacheScopes.User(request.UserId), ct);
        await _cache.InvalidateAsync(CacheScopes.Admin, ct);
    }
}