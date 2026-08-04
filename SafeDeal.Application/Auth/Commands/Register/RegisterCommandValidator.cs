using FluentValidation;

namespace SafeDeal.Application.Auth.Commands.Register;

public class RegisterCommandValidator : AbstractValidator<RegisterCommand>
{
    public RegisterCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(255);
        RuleFor(x => x.Password).NotEmpty().MinimumLength(8);
        RuleFor(x => x.PasswordConfirmation)
            .Equal(x => x.Password).WithMessage("Passwords do not match.");
        RuleFor(x => x.Role)
            .Must(r => r is "vendor" or "buyer")
            .WithMessage("Role must be 'vendor' or 'buyer'.");
    }
}