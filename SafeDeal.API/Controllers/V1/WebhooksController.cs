using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using MediatR;
using SafeDeal.Application.Transactions.Commands.PayTransaction;
using Stripe;
using Stripe.Forwarding;

namespace SafeDeal.API.Controllers.V1
{
    [ApiController]
    [Route("api/v1/[controller]")]
    public class WebhooksController : ControllerBase
    {
        private readonly IConfiguration _config;
        private readonly IMediator _mediator;

        public WebhooksController(IConfiguration config, IMediator mediator)
        {
            _config = config;
            _mediator = mediator;
        }

        [HttpPost("stripe")]
        public async Task<IActionResult> Stripe(CancellationToken ct)
        {
            var payload = await new StreamReader(Request.Body).ReadToEndAsync(ct);
            var signature = Request.Headers["Stripe-Signature"].ToString();
            var secret = _config["Stripe:WebhookSecret"]!;

            Console.WriteLine($"=== WEBHOOK DEBUG ===");
            Console.WriteLine($"Payload length: {payload.Length}");
            Console.WriteLine($"Signature: {(signature.Length > 50 ? signature[..50] : signature)}...");
            Console.WriteLine($"Secret: {(secret.Length > 20 ? secret[..20] : secret)}...");

            Event stripeEvent;
            try
            {
                stripeEvent = EventUtility.ConstructEvent(payload, signature, secret, throwOnApiVersionMismatch: false);
            }
            catch (StripeException ex)
            {
                Console.WriteLine($"Stripe error: {ex.Message}");
                return BadRequest(new { message = ex.Message });
            }

            if (stripeEvent.Type == EventTypes.CheckoutSessionCompleted)
            {
                var session = stripeEvent.Data.Object as Stripe.Checkout.Session;
                if (session?.Metadata != null &&
                    session.Metadata.TryGetValue("transaction_id", out var transactionId))
                {
                    await _mediator.Send(new PayTransactionCommand(
                        int.Parse(transactionId),
                        session.Id,
                        session.PaymentIntentId ?? ""), ct);
                }
            }

            return Ok(new { message = "Webhook handled." });
        }
    }
}
