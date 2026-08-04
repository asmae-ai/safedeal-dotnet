using Microsoft.Extensions.Configuration;
using SafeDeal.Domain.Interfaces.Services;
using Stripe;
using Stripe.Checkout;

namespace SafeDeal.Infrastructure.Services.Payment;

public class StripeService : IPaymentService
{
    private readonly IConfiguration _config;

    public StripeService(IConfiguration config)
    {
        _config = config;
        StripeConfiguration.ApiKey = config["Stripe:SecretKey"];
    }

    public async Task<(string CheckoutUrl, string SessionId)> CreateCheckoutSessionAsync(
        int transactionId, decimal amount, string currency, string title, CancellationToken ct = default)
    {
        var options = new SessionCreateOptions
        {
            PaymentMethodTypes = ["card"],
            LineItems =
            [
                new SessionLineItemOptions
                {
                    PriceData = new SessionLineItemPriceDataOptions
                    {
                        Currency = currency.ToLower(),
                        UnitAmount = (long)(amount * 100),
                        ProductData = new SessionLineItemPriceDataProductDataOptions
                        {
                            Name = title
                        }
                    },
                    Quantity = 1
                }
            ],
            Mode = "payment",
            SuccessUrl = _config["Stripe:SuccessUrl"],
            CancelUrl = _config["Stripe:CancelUrl"],
            Metadata = new Dictionary<string, string>
            {
                ["transaction_id"] = transactionId.ToString()
            }
        };

        var service = new SessionService();
        var session = await service.CreateAsync(options, cancellationToken: ct);
        return (session.Url, session.Id);
    }

    public async Task<bool> ValidateWebhookAsync(string payload, string signature, CancellationToken ct = default)
    {
        try
        {
            var secret = _config["Stripe:WebhookSecret"]!;
            EventUtility.ConstructEvent(payload, signature, secret);
            return true;
        }
        catch { return false; }
    }

    public async Task<string> GetPaymentIntentFromSessionAsync(string sessionId, CancellationToken ct = default)
    {
        var service = new SessionService();
        var session = await service.GetAsync(sessionId, cancellationToken: ct);
        return session.PaymentIntentId;
    }

    public async Task RefundAsync(string paymentIntentId, CancellationToken ct = default)
    {
        var options = new RefundCreateOptions { PaymentIntent = paymentIntentId };
        var service = new RefundService();
        await service.CreateAsync(options, cancellationToken: ct);
    }
}