using SafeDeal.Domain.Interfaces.Services;

namespace SafeDeal.Tests.Integration;

/// <summary>Stripe en doublure : enregistre les appels au lieu de les emettre.</summary>
public class FakePaymentService : IPaymentService
{
    public List<string> Refunds { get; } = [];
    public List<int> CheckoutSessions { get; } = [];
    public string? LastSuccessUrl { get; private set; }

    /// <summary>Permet de simuler un refus du prestataire de paiement.</summary>
    public bool RefundShouldFail { get; set; }

    public Task<(string CheckoutUrl, string SessionId)> CreateCheckoutSessionAsync(
        int transactionId, string secureToken, decimal amount, string currency, string title, CancellationToken ct = default)
    {
        CheckoutSessions.Add(transactionId);
        LastSuccessUrl = $"http://localhost:5173/pay/{secureToken}?payment=success";
        return Task.FromResult(($"https://checkout.stripe.test/{secureToken}", $"cs_test_{transactionId}"));
    }

    public Task<bool> ValidateWebhookAsync(string payload, string signature, CancellationToken ct = default)
        => Task.FromResult(true);

    public Task<string> GetPaymentIntentFromSessionAsync(string sessionId, CancellationToken ct = default)
        => Task.FromResult($"pi_test_{sessionId}");

    public Task RefundAsync(string paymentIntentId, CancellationToken ct = default)
    {
        if (RefundShouldFail) throw new InvalidOperationException("Refund refused by provider.");
        Refunds.Add(paymentIntentId);
        return Task.CompletedTask;
    }
}

/// <summary>Capture les e-mails pour pouvoir relire les codes envoyes.</summary>
public class FakeEmailService : IEmailService
{
    public List<(string To, string Subject, string Code)> Sent { get; } = [];

    public Task SendVerificationCodeAsync(string email, string name, string code, CancellationToken ct = default)
    { Sent.Add((email, "verification", code)); return Task.CompletedTask; }

    public Task SendPasswordResetAsync(string email, string name, string token, CancellationToken ct = default)
    { Sent.Add((email, "reset", token)); return Task.CompletedTask; }

    public Task SendOtpAsync(string email, string name, string code, CancellationToken ct = default)
    { Sent.Add((email, "otp", code)); return Task.CompletedTask; }

    public Task SendTransactionNotificationAsync(string email, string name, string message, CancellationToken ct = default)
    { Sent.Add((email, "transaction", message)); return Task.CompletedTask; }

    public string? LastOtpFor(string email)
        => Sent.LastOrDefault(s => s.To == email && s.Subject == "otp").Code;
}

public class FakeIdentityVerificationService : IIdentityVerificationService
{
    /// <summary>Permet de simuler un condense de charge utile invalide.</summary>
    public bool SignatureIsValid { get; set; } = true;

    /// <summary>Etat renvoye par le prestataire lorsqu'on l'interroge.</summary>
    public string ApplicantStatus { get; set; } = "pending";

    public List<int> Applicants { get; } = [];

    public Task<string> CreateApplicantAsync(int userId, string email, CancellationToken ct = default)
    {
        Applicants.Add(userId);
        return Task.FromResult($"applicant_{userId}");
    }

    public Task<bool> ValidateWebhookAsync(string payload, string signature, CancellationToken ct = default)
        => Task.FromResult(SignatureIsValid);

    public Task<string> GetApplicantStatusAsync(string applicantId, CancellationToken ct = default)
        => Task.FromResult(ApplicantStatus);
}
