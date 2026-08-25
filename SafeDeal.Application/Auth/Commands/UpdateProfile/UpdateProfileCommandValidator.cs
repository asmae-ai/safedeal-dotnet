using FluentValidation;

namespace SafeDeal.Application.Auth.Commands.UpdateProfile;

public class UpdateProfileCommandValidator : AbstractValidator<UpdateProfileCommand>
{
    public UpdateProfileCommandValidator()
    {
        // Les deux champs sont optionnels : seule leur forme est controlee quand
        // ils sont fournis, pour rester compatible avec une mise a jour partielle.
        RuleFor(x => x.Name)
            .MinimumLength(2).MaximumLength(100)
            .When(x => !string.IsNullOrWhiteSpace(x.Name));

        RuleFor(x => x.Phone)
            .Matches(@"^\+?[0-9]{9,15}$")
            .When(x => !string.IsNullOrWhiteSpace(x.Phone))
            .WithMessage("Phone must contain 9 to 15 digits.");
    }
}
