using FluentValidation;

namespace SafeDeal.Application.Transactions.Commands.PayTransaction;

public class PayTransactionCommandValidator : AbstractValidator<PayTransactionCommand>
{
    public PayTransactionCommandValidator()
    {
        RuleFor(x => x.TransactionId).GreaterThan(0);
        RuleFor(x => x.SessionId).NotEmpty().MaximumLength(255);
    }
}
