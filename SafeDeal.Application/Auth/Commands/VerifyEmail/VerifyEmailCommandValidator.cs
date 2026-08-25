using FluentValidation;

namespace SafeDeal.Application.Auth.Commands.VerifyEmail;

public class VerifyEmailCommandValidator : AbstractValidator<VerifyEmailCommand>
{
    public VerifyEmailCommandValidator()
    {
        // Le code est genere sur six chiffres : rejeter le reste evite d'aller
        // interroger le cache pour une valeur qui ne peut pas correspondre.
        RuleFor(x => x.Code)
            .NotEmpty()
            .Matches("^[0-9]{6}$").WithMessage("The code must contain 6 digits.");
    }
}
