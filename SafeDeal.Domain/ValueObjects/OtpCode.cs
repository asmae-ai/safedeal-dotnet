namespace SafeDeal.Domain.ValueObjects;

public record OtpCode
{
    public string Code { get; }
    public DateTime ExpiresAt { get; }
    public bool IsUsed { get; private set; }

    public OtpCode(string code, int expiryMinutes = 10)
    {
        if (string.IsNullOrWhiteSpace(code) || code.Length != 6)
            throw new ArgumentException("OTP must be 6 digits.");
        Code = code;
        ExpiresAt = DateTime.UtcNow.AddMinutes(expiryMinutes);
        IsUsed = false;
    }

    public bool IsExpired() => DateTime.UtcNow > ExpiresAt;
    public bool IsValid(string input) => !IsUsed && !IsExpired() && Code == input;
    public void MarkAsUsed() => IsUsed = true;
}