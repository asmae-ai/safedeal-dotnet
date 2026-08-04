using SafeDeal.Domain.Common;
using SafeDeal.Domain.Enums;

namespace SafeDeal.Domain.Entities;

public class User : BaseEntity, IAuditableEntity
{
    public string Name { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public string PasswordHash { get; private set; } = string.Empty;
    public string? Phone { get; private set; }
    public UserRole Role { get; private set; }
    public bool IsEmailVerified { get; private set; }
    public IdentityStatus IdentityStatus { get; private set; } = IdentityStatus.NotSubmitted;
    public decimal ReputationScore { get; private set; } = 0;
    public bool IsActive { get; private set; } = true;

    private User() { }

    public static User Create(string name, string email, string passwordHash, UserRole role, string? phone = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);

        return new User
        {
            Name = name.Trim(),
            Email = email.Trim().ToLower(),
            PasswordHash = passwordHash,
            Role = role,
            Phone = phone?.Trim(),
            IsEmailVerified = false
        };
    }

    public void VerifyEmail() => IsEmailVerified = true;
    public void UpdateIdentityStatus(IdentityStatus status)
    {
        IdentityStatus = status;
        UpdateTimestamp();
    }
    public void UpdatePhone(string phone)
    {
        Phone = phone.Trim();
        UpdateTimestamp();
    }
    public void UpdatePasswordHash(string hash)
    {
        PasswordHash = hash;
        UpdateTimestamp();
    }
    public void Deactivate()
    {
        IsActive = false;
        UpdateTimestamp();
    }
}