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

    /// <summary>Cree une transaction sous sequestre.</summary>
    /// <remarks>
    /// Reserve aux vendeurs dont l'identite est approuvee : le sequestre engage
    /// la plateforme, elle doit savoir qui encaisse.
    ///
    /// La reponse porte un `token` : c'est le lien de paiement a transmettre a
    /// l'acheteur, et la seule facon pour lui de retrouver la transaction.
    ///
    /// Reponse : `{ message, data }`.
    /// </remarks>
    /// <response code="200">Transaction creee.</response>
    /// <response code="403">Compte non vendeur, ou identite non verifiee.</response>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateTransactionRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(
            new CreateTransactionCommand(UserId, request.Title, request.Amount, request.Currency), ct);
        return Ok(new { message = "Transaction created.", data = result });
    }

    /// <param name="perPage">Optionnel, 15 par defaut, plafonne a 100.</param>
    /// <summary>Liste les transactions ou l'appelant est partie, de la plus recente a la plus ancienne.</summary>
    /// <remarks>
    /// Vendeur et acheteur voient chacun les siennes, jamais celles des autres.
    ///
    /// Reponse : `{ data, meta: { current_page, last_page, total } }`.
    /// </remarks>
    /// <param name="page">Page demandee, 1 par defaut. Une valeur hors bornes est ramenee a 1.</param>
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] int page = 1,
        [FromQuery(Name = "per_page")] int perPage = 15,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetTransactionsQuery(UserId, page, perPage), ct);
        return Ok(new
        {
            data = result.Data,
            meta = new { current_page = result.CurrentPage, last_page = result.LastPage, total = result.Total }
        });
    }

    /// <summary>Lit une transaction par son jeton securise.</summary>
    /// <remarks>
    /// Volontairement accessible sans authentification : c'est la page qu'ouvre
    /// un acheteur qui n'a pas encore de compte. Le jeton, imprevisible, tient
    /// lieu de droit d'acces — cette lecture ne passe jamais par le cache.
    /// </remarks>
    /// <param name="token">Jeton securise porte par le lien de paiement.</param>
    /// <response code="404">Jeton inconnu.</response>
    [HttpGet("{token}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetByToken(string token, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetTransactionByTokenQuery(token), ct);
        return Ok(new { data = result });
    }

    /// <summary>Rattache l'acheteur connecte a la transaction.</summary>
    /// <remarks>
    /// Une transaction ne se reclame qu'une fois, et jamais par son propre
    /// vendeur.
    /// </remarks>
    /// <param name="token">Jeton securise porte par le lien de paiement.</param>
    /// <response code="422">Transaction deja reclamee, ou reclamee par son vendeur.</response>
    [HttpPost("{token}/claim")]
    public async Task<IActionResult> Claim(string token, CancellationToken ct)
    {
        var result = await _mediator.Send(new ClaimTransactionCommand(token, UserId), ct);
        return Ok(new { message = "Transaction claimed.", data = result });
    }

    /// <summary>Ouvre une session de paiement Stripe.</summary>
    /// <remarks>
    /// Rend l'URL de paiement hebergee. L'encaissement n'est acquis qu'au retour
    /// du webhook Stripe, jamais a la redirection du navigateur.
    /// </remarks>
    /// <response code="422">Transaction deja payee ou dans un etat qui n'accepte plus de paiement.</response>
    [HttpPost("{id:int}/checkout")]
    public async Task<IActionResult> Checkout(int id, CancellationToken ct)
    {
        var result = await _mediator.Send(new CheckoutTransactionCommand(id, UserId), ct);
        return Ok(new { checkout_url = result.CheckoutUrl, session_id = result.SessionId });
    }

    /// <summary>Declare la commande expediee.</summary>
    /// <remarks>Reserve au vendeur de la transaction. Le numero de suivi et le transporteur sont exiges.</remarks>
    /// <response code="403">L'appelant n'est pas le vendeur.</response>
    /// <response code="422">Transition impossible depuis l'etat courant.</response>
    [HttpPost("{id:int}/ship")]
    public async Task<IActionResult> Ship(int id, [FromBody] ShipRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(
            new ShipTransactionCommand(id, UserId, request.TrackingNumber, request.Carrier), ct);
        return Ok(new { data = result });
    }

    /// <summary>Constate la reception de la commande.</summary>
    /// <remarks>
    /// Reserve a l'acheteur : c'est son constat qui ouvre la liberation des
    /// fonds, le vendeur ne peut pas se payer lui-meme.
    /// </remarks>
    /// <response code="403">L'appelant n'est pas l'acheteur.</response>
    [HttpPost("{id:int}/deliver")]
    public async Task<IActionResult> Deliver(int id, CancellationToken ct)
    {
        var result = await _mediator.Send(new DeliverTransactionCommand(id, UserId), ct);
        return Ok(new { data = result });
    }

    /// <summary>Cloture la transaction et libere les fonds vers le vendeur.</summary>
    /// <remarks>Reserve a l'acheteur, et seulement depuis l'etat « livree ».</remarks>
    /// <response code="403">L'appelant n'est pas l'acheteur.</response>
    /// <response code="422">Transition impossible depuis l'etat courant.</response>
    [HttpPost("{id:int}/close")]
    public async Task<IActionResult> Close(int id, CancellationToken ct)
    {
        var result = await _mediator.Send(new CloseTransactionCommand(id, UserId), ct);
        return Ok(new { data = result });
    }

    /// <summary>Annule la transaction.</summary>
    /// <remarks>
    /// Ouverte aux deux parties. Si un paiement a deja ete encaisse, le
    /// remboursement est emis avant l'ecriture du statut : une annulation n'est
    /// jamais affichee tant que l'argent n'est pas reparti.
    /// </remarks>
    /// <response code="403">L'appelant n'est ni le vendeur ni l'acheteur.</response>
    /// <response code="422">Remboursement refuse par le prestataire, ou transition impossible.</response>
    [HttpPatch("{id:int}/cancel")]
    public async Task<IActionResult> Cancel(int id, CancellationToken ct)
    {
        var result = await _mediator.Send(new CancelTransactionCommand(id, UserId), ct);
        return Ok(new { message = "Transaction cancelled.", data = result });
    }
}

public record CreateTransactionRequest(string Title, decimal Amount, string Currency);
public record ShipRequest(string TrackingNumber, string Carrier);