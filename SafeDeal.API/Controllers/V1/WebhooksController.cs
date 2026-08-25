using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MediatR;
using SafeDeal.Application.Transactions.Commands.PayTransaction;
using System.Text.Json;
using SafeDeal.Application.Identity.Commands.SyncVerification;
using SafeDeal.Domain.Interfaces.Services;
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

        /// <summary>
        /// Decision de verification d'identite emise par Sumsub. Sans cet endpoint,
        /// le dossier cree a la soumission n'etait jamais relu et chaque decision
        /// devait etre ressaisie a la main dans l'ecran admin.
        /// </summary>
        [HttpPost("sumsub")]
        public async Task<IActionResult> Sumsub(
            [FromServices] IIdentityVerificationService identityService,
            CancellationToken ct)
        {
            var payload = await new StreamReader(Request.Body).ReadToEndAsync(ct);
            var digest = Request.Headers["X-Payload-Digest"].ToString();

            if (!await identityService.ValidateWebhookAsync(payload, digest, ct))
            {
                _logger.LogWarning("Rejected Sumsub webhook: invalid digest.");
                return BadRequest(new { message = "Invalid signature." });
            }

            SumsubEvent? evt;
            try
            {
                evt = JsonSerializer.Deserialize<SumsubEvent>(payload,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Malformed Sumsub webhook payload.");
                return BadRequest(new { message = "Malformed payload." });
            }

            // Seule la revue finale porte une decision ; les autres evenements du
            // cycle sont acquittes sans effet.
            if (evt?.ReviewResult?.ReviewAnswer is null || evt.ApplicantId is null)
                return Ok(new { message = "Ignored: no decision in this event." });

            await _mediator.Send(new SyncVerificationCommand(
                evt.ApplicantId,
                evt.ExternalUserId,
                evt.ReviewResult.ReviewAnswer,
                evt.ReviewResult.ModerationComment), ct);

            return Ok(new { message = "Webhook handled." });
        }
    }

    public record SumsubReviewResult(string? ReviewAnswer, string? ModerationComment);
    public record SumsubEvent(string? ApplicantId, string? ExternalUserId, string? Type, SumsubReviewResult? ReviewResult);
}
