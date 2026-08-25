namespace SafeDeal.Domain.Interfaces.Services;

public interface IPaymentService
{
    Task<(string CheckoutUrl, string SessionId)> CreateCheckoutSessionAsync(int transactionId, string secureToken, decimal amount, string currency, string title, CancellationToken ct = default);
    Task<bool> ValidateWebhookAsync(string payload, string signature, CancellationToken ct = default);
    Task<string> GetPaymentIntentFromSessionAsync(string sessionId, CancellationToken ct = default);
    Task RefundAsync(string paymentIntentId, CancellationToken ct = default);
}