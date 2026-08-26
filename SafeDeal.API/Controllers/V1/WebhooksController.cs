using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
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

        /// <summary>Notification d'encaissement emise par Stripe.</summary>
        /// <remarks>
        /// Authentifie par la signature `Stripe-Signature`, pas par un jeton :
        /// l'appelant est Stripe, pas un utilisateur.
        ///
        /// Un rejeu sur une transaction deja payee est acquitte sans effet :
        /// Stripe reemet jusqu'a obtenir un 2xx. Un corps illisible sort en 400
        /// et non en 500, car aucun rejeu ne le rendra valide.
        /// </remarks>
        /// <response code="200">Evenement traite, ou ignore faute de reference exploitable.</response>
        /// <response code="400">Signature invalide ou charge utile malformee.</response>
        [HttpPost("stripe")]
        [EnableRateLimiting("webhooks")]
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

        /// <summary>Decision de verification d'identite emise par Sumsub.</summary>
        /// <remarks>
        /// Authentifie par le condense `X-Payload-Digest`. Seule la revue finale
        /// porte une decision (`GREEN` ou `RED`) ; les autres evenements du cycle
        /// sont acquittes sans effet, comme les rejeux d'une decision deja
        /// appliquee.
        /// </remarks>
        /// <response code="200">Decision appliquee, ou evenement sans decision acquitte.</response>
        /// <response code="400">Condense invalide ou charge utile malformee.</response>
        [HttpPost("sumsub")]
        [EnableRateLimiting("webhooks")]
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
