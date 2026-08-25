using FluentValidation;

namespace SafeDeal.Application.Disputes.Commands.ResolveDispute;

public class ResolveDisputeCommandValidator : AbstractValidator<ResolveDisputeCommand>
{
    public ResolveDisputeCommandValidator()
    {
        RuleFor(x => x.DisputeId).GreaterThan(0);

        // Meme message et meme cle d'erreur que le controle deja present dans le
        // handler : le contrat vu du client ne change pas.
        RuleFor(x => x.Decision)
            .NotEmpty()
            .Must(d => d is "resolved" or "refunded")
            .WithMessage("Decision must be 'resolved' or 'refunded'.");

        RuleFor(x => x.Note).MaximumLength(2000);
    }
}
