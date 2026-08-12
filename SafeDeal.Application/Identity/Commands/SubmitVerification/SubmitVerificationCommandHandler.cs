using MediatR;
using SafeDeal.Application.Common.Exceptions;
using SafeDeal.Domain.Entities;
using SafeDeal.Domain.Enums;
using SafeDeal.Domain.Interfaces.Repositories;
using SafeDeal.Domain.Interfaces.Services;

namespace SafeDeal.Application.Identity.Commands.SubmitVerification;

public class SubmitVerificationCommandHandler : IRequestHandler<SubmitVerificationCommand>
{
    private readonly IIdentityVerificationRepository _verifications;
    private readonly IUserRepository _users;
    private readonly IIdentityVerificationService _sumsub;

    public SubmitVerificationCommandHandler(
        IIdentityVerificationRepository verifications,
        IUserRepository users,
        IIdentityVerificationService sumsub)
    {
        _verifications = verifications;
        _users = users;
        _sumsub = sumsub;
    }

    public async Task Handle(SubmitVerificationCommand request, CancellationToken ct)
    {
        var user = await _users.GetByIdAsync(request.UserId, ct)
            ?? throw new NotFoundException("User", request.UserId);

        if (user.Role != UserRole.Vendor)
            throw new ForbiddenException("Only vendors can submit identity verification.");

        var existing = await _verifications.GetByUserIdAsync(request.UserId, ct);
        if (existing is not null && existing.Status == IdentityStatus.Pending)
            throw new ValidationException(new Dictionary<string, string[]>
            {
                ["verification"] = ["A verification is already pending."]
            });

        // Créer un applicant Sumsub
        string? applicantId = null;
        try
        {
            applicantId = await _sumsub.CreateApplicantAsync(request.UserId, user.Email, ct);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"=== SUMSUB ERROR: {ex.Message} ===");
        }
        var verification = IdentityVerification.Create(
            request.UserId,
            request.DocumentType,
            request.DocumentFrontPath,
            request.SelfiePath);

        if (applicantId is not null)
            verification.SetSumsubApplicantId(applicantId);

        await _verifications.AddAsync(verification, ct);
    }
}