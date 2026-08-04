using MediatR;
using SafeDeal.Application.Identity.DTOs;
using SafeDeal.Domain.Interfaces.Repositories;

namespace SafeDeal.Application.Identity.Queries.GetVerificationStatus;

public class GetVerificationStatusQueryHandler : IRequestHandler<GetVerificationStatusQuery, IdentityStatusDto>
{
    private readonly IIdentityVerificationRepository _verifications;
    public GetVerificationStatusQueryHandler(IIdentityVerificationRepository verifications)
        => _verifications = verifications;

    public async Task<IdentityStatusDto> Handle(GetVerificationStatusQuery request, CancellationToken ct)
    {
        var verification = await _verifications.GetByUserIdAsync(request.UserId, ct);
        if (verification is null)
            return new IdentityStatusDto("not_submitted", null);

        return new IdentityStatusDto(
            verification.Status.ToString().ToLower(),
            verification.CreatedAt.ToString("o"));
    }
}