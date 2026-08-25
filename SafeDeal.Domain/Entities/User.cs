using BC = BCrypt.Net.BCrypt;
using SafeDeal.Domain.Common;
using SafeDeal.Domain.Enums;

namespace SafeDeal.Domain.Entities;

public class User : BaseEntity, IAuditableEntity
{
    public string Name { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public string PasswordHash { get; private set; } = string.Empty;
    public string? Phone { get; private set; }
    public string? AvatarPath { get; private set; }
    public UserRole Role { get; private set; }
    public bool IsEmailVerified { get; private set; }
    public IdentityStatus IdentityStatus { get; private set; } = IdentityStatus.NotSubmitted;
    public decimal ReputationScore { get; private set; } = 0;
    public bool IsActive { get; private set; } = true;
    /// <summary>Quand elle est active, la connexion exige un code envoyé par e-mail.</summary>
    public bool TwoFactorEnabled { get; private set; }

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

    public void SetTwoFactor(bool enabled)
    {
        TwoFactorEnabled = enabled;
        UpdateTimestamp();
    }

    /// <summary>Récompense une transaction menée à son terme, plafonnée à 5.</summary>
    public void RegisterSuccessfulTransaction()
    {
        ReputationScore = Math.Min(5m, Math.Round(ReputationScore + 0.1m, 2));
        UpdateTimestamp();
    }

    /// <summary>Pénalise un litige tranché contre l'utilisateur, plancher à 0.</summary>
    public void RegisterFailedTransaction()
    {
        ReputationScore = Math.Max(0m, Math.Round(ReputationScore - 0.3m, 2));
        UpdateTimestamp();
    }

    public void UpdateProfile(string? name, string? phone)
    {
        if (!string.IsNullOrWhiteSpace(name))
            Name = name.Trim();
        if (!string.IsNullOrWhiteSpace(phone))
            Phone = phone.Trim();
        UpdateTimestamp();
    }

    public void UpdateAvatar(string path)
    {
        AvatarPath = path;
        UpdateTimestamp();
    }

    public bool VerifyPassword(string password)
    {
        return BC.Verify(password, PasswordHash);
    }

    public void ChangePassword(string newPassword)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(newPassword);
        PasswordHash = BC.HashPassword(newPassword);
        UpdateTimestamp();
    }
}
