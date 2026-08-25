using FluentValidation;

namespace SafeDeal.Application.Transactions.Commands.ClaimTransaction;

public class ClaimTransactionCommandValidator : AbstractValidator<ClaimTransactionCommand>
{
    public ClaimTransactionCommandValidator()
    {
        RuleFor(x => x.Token).NotEmpty().MaximumLength(128);
        RuleFor(x => x.BuyerId).GreaterThan(0);
    }
}
