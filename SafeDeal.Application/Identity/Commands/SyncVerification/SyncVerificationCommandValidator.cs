using FluentValidation;

namespace SafeDeal.Application.Identity.Commands.SyncVerification;

public class SyncVerificationCommandValidator : AbstractValidator<SyncVerificationCommand>
{
    public SyncVerificationCommandValidator()
    {
        RuleFor(x => x.ApplicantId).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Answer).NotEmpty().MaximumLength(20);
        RuleFor(x => x.Reason).MaximumLength(500);
    }
}
