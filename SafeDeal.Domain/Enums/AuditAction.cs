namespace SafeDeal.Domain.Enums;

/// <summary>
/// Actions tracées. Volontairement fermée : un journal d'audit sert à répondre
/// à « qui a fait quoi », ce qui suppose un vocabulaire stable dans le temps.
/// </summary>
public enum AuditAction
{
    // --- Authentification ---
    Login,
    LoginFailed,
    Logout,
    TokenRefreshed,
    PasswordChanged,
    PasswordResetRequested,
    PasswordReset,
    EmailVerified,
    TwoFactorEnabled,
    TwoFactorDisabled,
    TwoFactorVerified,
    UserRegistered,
    ProfileUpdated,
    AvatarUpdated,

    // --- Transactions ---
    TransactionCreated,
    TransactionClaimed,
    CheckoutStarted,
    PaymentReceived,
    TransactionShipped,
    TransactionDelivered,
    TransactionClosed,
    TransactionCancelled,
    TransactionRefunded,

    // --- Litiges ---
    DisputeOpened,
    DisputeEvidenceSubmitted,
    DisputeResolved,

    // --- Vérification d'identité ---
    IdentitySubmitted,
    IdentityApproved,
    IdentityRejected,
    IdentitySyncedFromProvider
}
