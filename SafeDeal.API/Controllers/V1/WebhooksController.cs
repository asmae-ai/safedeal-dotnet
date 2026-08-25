using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MediatR;
using SafeDeal.Application.Transactions.Commands.PayTransaction;
using Stripe;

namespace SafeDeal.API.Controllers.V1
{
    [ApiController]
    [Route("api/v1/[controller]")]
    public class WebhooksController : ControllerBase
    {
        private readonly IConfiguration _config;
        private readonly IMediator _mediator;
        private readonly ILogger<WebhooksController> _logger;

        public WebhooksController(IConfiguration config, IMediator mediator, ILogger<WebhooksController> logger)
        {
            _config = config;
            _mediator = mediator;
            _logger = logger;
        }

        [HttpPost("stripe")]
        public async Task<IActionResult> Stripe(CancellationToken ct)
        {
            var payload = await new StreamReader(Request.Body).ReadToEndAsync(ct);
            var signature = Request.Headers["Stripe-Signature"].ToString();
            var secret = _config["Stripe:WebhookSecret"]!;

            Event stripeEvent;
            try
            {
                stripeEvent = EventUtility.ConstructEvent(payload, signature, secret, throwOnApiVersionMismatch: false);
            }
            catch (StripeException ex)
            {
                // Signature invalide : la requête ne vient pas de Stripe.
                _logger.LogWarning("Rejected Stripe webhook: {Reason}", ex.Message);
                return BadRequest(new { message = "Invalid signature." });
            }
            catch (Exception ex)
            {
                // Payload illisible. Un 500 ferait réessayer Stripe indéfiniment,
                // alors qu'aucun rejeu ne rendra ce corps de requête valide.
                _logger.LogError(ex, "Malformed Stripe webhook payload.");
                return BadRequest(new { message = "Malformed payload." });
            }

            if (stripeEvent.Type == EventTypes.CheckoutSessionCompleted)
            {
                var session = stripeEvent.Data.Object as Stripe.Checkout.Session;

                if (session?.Metadata is null ||
                    !session.Metadata.TryGetValue("transaction_id", out var rawId) ||
                    !int.TryParse(rawId, out var transactionId))
                {
                    _logger.LogWarning("Stripe session {SessionId} carries no usable transaction_id.", session?.Id);
                    return Ok(new { message = "Ignored: no transaction reference." });
                }

                await _mediator.Send(new PayTransactionCommand(
                    transactionId,
                    session.Id,
                    session.PaymentIntentId ?? ""), ct);
            }

            return Ok(new { message = "Webhook handled." });
        }
    }
}
