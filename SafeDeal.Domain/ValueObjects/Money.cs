namespace SafeDeal.Domain.ValueObjects;

public record Money
{
    public decimal Amount { get; }
    public string Currency { get; }

    public Money(decimal amount, string currency)
    {
        if (amount < 0) throw new ArgumentException("Amount cannot be negative.");
        if (string.IsNullOrWhiteSpace(currency)) throw new ArgumentException("Currency is required.");
        Amount = Math.Round(amount, 2);
        Currency = currency.ToUpper();
    }

    public override string ToString() => $"{Amount:F2} {Currency}";
}