using FluentValidation;

namespace SafeDeal.Application.Admin.Commands.ApproveIdentity;

public class ApproveIdentityCommandValidator : AbstractValidator<ApproveIdentityCommand>
{
    public ApproveIdentityCommandValidator()
    {
        RuleFor(x => x.UserId).GreaterThan(0);
    }
}
