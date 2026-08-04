namespace SafeDeal.Domain.ValueObjects;

public record SecureToken
{
    public string Value { get; }

    public SecureToken() => Value = Guid.NewGuid().ToString("N");
    public SecureToken(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Token cannot be empty.");
        Value = value;
    }

    public override string ToString() => Value;
}