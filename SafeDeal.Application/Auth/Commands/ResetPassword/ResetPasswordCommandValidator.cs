using FluentValidation;

namespace SafeDeal.Application.Auth.Commands.ResetPassword;

public class ResetPasswordCommandValidator : AbstractValidator<ResetPasswordCommand>
{
    public ResetPasswordCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(255);
        RuleFor(x => x.Token).NotEmpty();
        RuleFor(x => x.Password).NotEmpty().MinimumLength(8).MaximumLength(128);
        RuleFor(x => x.PasswordConfirmation)
            .Equal(x => x.Password).WithMessage("Passwords do not match.");
    }
}
