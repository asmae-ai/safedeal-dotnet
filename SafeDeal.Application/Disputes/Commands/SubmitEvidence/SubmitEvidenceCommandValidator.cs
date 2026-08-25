using FluentValidation;

namespace SafeDeal.Application.Disputes.Commands.SubmitEvidence;

public class SubmitEvidenceCommandValidator : AbstractValidator<SubmitEvidenceCommand>
{
    public SubmitEvidenceCommandValidator()
    {
        RuleFor(x => x.TransactionId).GreaterThan(0);
        RuleFor(x => x.Description)
            .NotEmpty()
            .MinimumLength(10).WithMessage("Please describe your response (10 characters minimum).")
            .MaximumLength(2000);
    }
}
