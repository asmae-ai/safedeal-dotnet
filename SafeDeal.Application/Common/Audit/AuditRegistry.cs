using SafeDeal.Domain.Enums;

namespace SafeDeal.Application.Common.Audit;

/// <param name="EntityType">Type d'agrégat visé, pour retrouver l'historique d'une entité.</param>
/// <param name="SubjectProperty">
/// Propriété du command portant un identifiant lisible (e-mail). Whitelist explicite :
/// aucune autre propriété n'est lue, ce qui garantit qu'un mot de passe ou un code
/// ne peut pas se retrouver dans le journal par inadvertance.
/// </param>
public record AuditDescriptor(
    AuditAction Action,
    string? EntityType = null,
    string? SubjectProperty = null);

/// <summary>
/// Table des commandes tracées. Centralisée plutôt que dispersée dans les
/// handlers : la liste de ce qui est audité se lit d'un seul coup d'œil, et
/// ajouter une commande sensible sans l'auditer devient visible en revue.
/// </summary>
public static class AuditRegistry
{
    private static readonly Dictionary<string, AuditDescriptor> Descriptors = new()
    {
        // --- Authentification ---
        ["LoginCommand"] = new(AuditAction.Login, "User", "Email"),
        ["LogoutCommand"] = new(AuditAction.Logout, "User"),
        ["RefreshTokenCommand"] = new(AuditAction.TokenRefreshed, "User"),
        ["RegisterCommand"] = new(AuditAction.UserRegistered, "User", "Email"),
        ["ChangePasswordCommand"] = new(AuditAction.PasswordChanged, "User"),
        ["ForgotPasswordCommand"] = new(AuditAction.PasswordResetRequested, "User", "Email"),
        ["ResetPasswordCommand"] = new(AuditAction.PasswordReset, "User", "Email"),
        ["VerifyEmailCommand"] = new(AuditAction.EmailVerified, "User"),
        ["VerifyTwoFactorCommand"] = new(AuditAction.TwoFactorVerified, "User", "Email"),
        ["SetTwoFactorCommand"] = new(AuditAction.TwoFactorEnabled, "User"),
        ["UpdateProfileCommand"] = new(AuditAction.ProfileUpdated, "User"),
        ["UploadAvatarCommand"] = new(AuditAction.AvatarUpdated, "User"),

        // --- Transactions ---
        ["CreateTransactionCommand"] = new(AuditAction.TransactionCreated, "Transaction"),
        ["ClaimTransactionCommand"] = new(AuditAction.TransactionClaimed, "Transaction"),
        ["CheckoutTransactionCommand"] = new(AuditAction.CheckoutStarted, "Transaction"),
        ["PayTransactionCommand"] = new(AuditAction.PaymentReceived, "Transaction"),
        ["ShipTransactionCommand"] = new(AuditAction.TransactionShipped, "Transaction"),
        ["DeliverTransactionCommand"] = new(AuditAction.TransactionDelivered, "Transaction"),
        ["CloseTransactionCommand"] = new(AuditAction.TransactionClosed, "Transaction"),
        ["CancelTransactionCommand"] = new(AuditAction.TransactionCancelled, "Transaction"),

        // --- Litiges ---
        ["OpenDisputeCommand"] = new(AuditAction.DisputeOpened, "Dispute"),
        ["SubmitEvidenceCommand"] = new(AuditAction.DisputeEvidenceSubmitted, "Dispute"),
        ["ResolveDisputeCommand"] = new(AuditAction.DisputeResolved, "Dispute"),

        // --- Vérification d'identité ---
        ["SubmitVerificationCommand"] = new(AuditAction.IdentitySubmitted, "IdentityVerification"),
        ["ApproveIdentityCommand"] = new(AuditAction.IdentityApproved, "IdentityVerification"),
        ["RejectIdentityCommand"] = new(AuditAction.IdentityRejected, "IdentityVerification"),
        ["SyncVerificationCommand"] = new(AuditAction.IdentitySyncedFromProvider, "IdentityVerification"),
    };

    public static AuditDescriptor? For(Type requestType)
        => Descriptors.GetValueOrDefault(requestType.Name);

    /// <summary>Propriétés autorisées à identifier l'auteur de l'action.</summary>
    public static readonly string[] UserIdProperties =
        ["UserId", "BuyerId", "VendorId", "OpenedByUserId"];

    /// <summary>Propriétés autorisées à identifier l'entité visée.</summary>
    public static readonly string[] EntityIdProperties =
        ["TransactionId", "DisputeId", "Id"];
}
