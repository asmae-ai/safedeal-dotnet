using FluentValidation;

namespace SafeDeal.Application.Admin.Commands.RejectIdentity;

public class RejectIdentityCommandValidator : AbstractValidator<RejectIdentityCommand>
{
    public RejectIdentityCommandValidator()
    {
        RuleFor(x => x.UserId).GreaterThan(0);

        // Le motif est communique a l'utilisateur : un rejet sans explication le
        // laisse sans moyen de corriger sa demande.
        RuleFor(x => x.Reason).NotEmpty().MinimumLength(5).MaximumLength(500);
    }
}
