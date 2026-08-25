using FluentValidation;

namespace SafeDeal.Application.Auth.Commands.VerifyTwoFactor;

public class VerifyTwoFactorCommandValidator : AbstractValidator<VerifyTwoFactorCommand>
{
    public VerifyTwoFactorCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(255);
        RuleFor(x => x.Code)
            .NotEmpty()
            .Matches("^[0-9]{6}$").WithMessage("The code must contain 6 digits.");
    }
}
