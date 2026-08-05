using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SafeDeal.Application.Transactions.Commands.CancelTransaction;
using SafeDeal.Application.Transactions.Commands.CheckoutTransaction;
using SafeDeal.Application.Transactions.Commands.ClaimTransaction;
using SafeDeal.Application.Transactions.Commands.CloseTransaction;
using SafeDeal.Application.Transactions.Commands.CreateTransaction;
using SafeDeal.Application.Transactions.Commands.DeliverTransaction;
using SafeDeal.Application.Transactions.Commands.ShipTransaction;
using SafeDeal.Application.Transactions.Queries.GetTransactionByToken;
using SafeDeal.Application.Transactions.Queries.GetTransactions;
using System.Security.Claims;

namespace SafeDeal.API.Controllers.V1;

[ApiController]
[Route("api/v1/transactions")]
[Authorize]
public class TransactionsController : ControllerBase
{
    private readonly IMediator _mediator;
    public TransactionsController(IMediator mediator) => _mediator = mediator;

    private int UserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateTransactionRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(
            new CreateTransactionCommand(UserId, request.Title, request.Amount, request.Currency), ct);
        return Ok(new { message = "Transaction created.", data = result });
    }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] int page = 1, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetTransactionsQuery(UserId, page), ct);
        return Ok(new
        {
            data = result.Data,
            meta = new { current_page = result.CurrentPage, last_page = result.LastPage, total = result.Total }
        });
    }

    [HttpGet("{token}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetByToken(string token, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetTransactionByTokenQuery(token), ct);
        return Ok(new { data = result });
    }

    [HttpPost("{token}/claim")]
    public async Task<IActionResult> Claim(string token, CancellationToken ct)
    {
        var result = await _mediator.Send(new ClaimTransactionCommand(token, UserId), ct);
        return Ok(new { message = "Transaction claimed.", data = result });
    }

    [HttpPost("{id:int}/checkout")]
    public async Task<IActionResult> Checkout(int id, CancellationToken ct)
    {
        var result = await _mediator.Send(new CheckoutTransactionCommand(id, UserId), ct);
        return Ok(new { checkout_url = result.CheckoutUrl, session_id = result.SessionId });
    }

    [HttpPost("{id:int}/ship")]
    public async Task<IActionResult> Ship(int id, [FromBody] ShipRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(
            new ShipTransactionCommand(id, UserId, request.TrackingNumber, request.Carrier), ct);
        return Ok(new { data = result });
    }

    [HttpPost("{id:int}/deliver")]
    public async Task<IActionResult> Deliver(int id, CancellationToken ct)
    {
        var result = await _mediator.Send(new DeliverTransactionCommand(id, UserId), ct);
        return Ok(new { data = result });
    }

    [HttpPost("{id:int}/close")]
    public async Task<IActionResult> Close(int id, CancellationToken ct)
    {
        var result = await _mediator.Send(new CloseTransactionCommand(id, UserId), ct);
        return Ok(new { data = result });
    }

    [HttpPatch("{id:int}/cancel")]
    public async Task<IActionResult> Cancel(int id, CancellationToken ct)
    {
        var result = await _mediator.Send(new CancelTransactionCommand(id, UserId), ct);
        return Ok(new { message = "Transaction cancelled.", data = result });
    }
}

public record CreateTransactionRequest(string Title, decimal Amount, string Currency);
public record ShipRequest(string TrackingNumber, string Carrier);