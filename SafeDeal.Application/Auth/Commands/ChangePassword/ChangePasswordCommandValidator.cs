using FluentValidation;

namespace SafeDeal.Application.Auth.Commands.ChangePassword;

public class ChangePasswordCommandValidator : AbstractValidator<ChangePasswordCommand>
{
    public ChangePasswordCommandValidator()
    {
        RuleFor(x => x.CurrentPassword).NotEmpty();
        RuleFor(x => x.NewPassword)
            .NotEmpty()
            .MinimumLength(8).WithMessage("Password must be at least 8 characters.")
            .MaximumLength(128);

        // Un changement qui ne change rien donne une fausse impression de securite.
        RuleFor(x => x.NewPassword)
            .NotEqual(x => x.CurrentPassword)
            .WithMessage("The new password must differ from the current one.");
    }
}
