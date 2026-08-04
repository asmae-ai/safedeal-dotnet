using FluentValidation;

namespace SafeDeal.Application.Identity.Commands.SubmitVerification;

public class SubmitVerificationCommandValidator : AbstractValidator<SubmitVerificationCommand>
{
    private static readonly string[] AllowedTypes = ["cin", "passport"];

    public SubmitVerificationCommandValidator()
    {
        RuleFor(x => x.DocumentType)
            .Must(t => AllowedTypes.Contains(t.ToLower()))
            .WithMessage("Document type must be 'cin' or 'passport'.");
        RuleFor(x => x.DocumentFrontPath).NotEmpty();
        RuleFor(x => x.SelfiePath).NotEmpty();
    }
}