using MediatR;
using Microsoft.AspNetCore.Mvc;
using SafeDeal.Application.Transactions.Commands.PayTransaction;
using SafeDeal.Domain.Interfaces.Services;
using Stripe;

namespace SafeDeal.API.Controllers.V1;

[ApiController]
[Route("api/v1/webhooks")]
public class WebhooksController : ControllerBase
{
    private readonly IPaymentService _paymentService;
    private readonly IMediator _mediator;
    private readonly IConfiguration _config;

    public WebhooksController(IPaymentService paymentService, IMediator mediator, IConfiguration config)
    {
        _paymentService = paymentService;
        _mediator = mediator;
        _config = config;
    }

    [HttpPost("stripe")]
    public async Task<IActionResult> Stripe(CancellationToken ct)
    {
        var payload = await new StreamReader(Request.Body).ReadToEndAsync(ct);
        var signature = Request.Headers["Stripe-Signature"].ToString();

        var isValid = await _paymentService.ValidateWebhookAsync(payload, signature, ct);
        if (!isValid) return BadRequest(new { message = "Invalid webhook signature." });

        var stripeEvent = EventUtility.ParseEvent(payload);

        if (stripeEvent.Type == EventTypes.CheckoutSessionCompleted)
        {
            var session = stripeEvent.Data.Object as Stripe.Checkout.Session;
            if (session?.Metadata.TryGetValue("transaction_id", out var transactionId) == true)
            {
                await _mediator.Send(new PayTransactionCommand(
                    int.Parse(transactionId),
                    session.Id,
                    session.PaymentIntentId), ct);
            }
        }

        return Ok();
    }

    [HttpPost("sumsub")]
    public async Task<IActionResult> Sumsub(
        [FromServices] IIdentityVerificationService sumsubService,
        CancellationToken ct)
    {
        var payload = await new StreamReader(Request.Body).ReadToEndAsync(ct);
        var signature = Request.Headers["X-Payload-Digest"].ToString();

        var isValid = await sumsubService.ValidateWebhookAsync(payload, signature, ct);
        if (!isValid) return BadRequest(new { message = "Invalid webhook signature." });

        return Ok();
    }
}