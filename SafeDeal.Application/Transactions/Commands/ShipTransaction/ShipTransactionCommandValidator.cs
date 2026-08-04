using FluentValidation;

namespace SafeDeal.Application.Transactions.Commands.ShipTransaction;

public class ShipTransactionCommandValidator : AbstractValidator<ShipTransactionCommand>
{
    public ShipTransactionCommandValidator()
    {
        RuleFor(x => x.TrackingNumber).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Carrier).NotEmpty().MaximumLength(100);
    }
}