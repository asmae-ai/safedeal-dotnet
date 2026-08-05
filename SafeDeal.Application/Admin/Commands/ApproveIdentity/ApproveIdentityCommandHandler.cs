using MediatR;
using SafeDeal.Application.Common.Exceptions;
using SafeDeal.Domain.Interfaces.Repositories;

namespace SafeDeal.Application.Admin.Commands.ApproveIdentity;

public class ApproveIdentityCommandHandler : IRequestHandler<ApproveIdentityCommand>
{
    private readonly IIdentityVerificationRepository _verifications;
    private readonly IUserRepository _users;

    public ApproveIdentityCommandHandler(
        IIdentityVerificationRepository verifications,
        IUserRepository users)
    {
        _verifications = verifications;
        _users = users;
    }

    public async Task Handle(ApproveIdentityCommand request, CancellationToken ct)
    {
        var verification = await _verifications.GetByUserIdAsync(request.UserId, ct)
            ?? throw new NotFoundException("IdentityVerification", request.UserId);

        var user = await _users.GetByIdAsync(request.UserId, ct)
            ?? throw new NotFoundException("User", request.UserId);

        verification.Approve();
        user.UpdateIdentityStatus(Domain.Enums.IdentityStatus.Approved);

        await _verifications.UpdateAsync(verification, ct);
        await _users.UpdateAsync(user, ct);
    }
}