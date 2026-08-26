using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SafeDeal.Application.Dashboard.Queries.GetAdminDashboard;
using SafeDeal.Application.Dashboard.Queries.GetBuyerDashboard;
using SafeDeal.Application.Dashboard.Queries.GetVendorDashboard;
using System.Security.Claims;

namespace SafeDeal.API.Controllers.V1;

[ApiController]
[Route("api/v1/dashboard")]
[Authorize]
public class DashboardController : ControllerBase
{
    private readonly IMediator _mediator;
    public DashboardController(IMediator mediator) => _mediator = mediator;

    private int UserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    /// <summary>Tableau de bord du vendeur connecte.</summary>
    /// <remarks>
    /// Agregats de lecture : chiffre d'affaires libere, fonds encore en
    /// sequestre, commission, commandes a traiter, courbe sur douze mois.
    ///
    /// Servi depuis le cache, avec une duree de vie courte et une invalidation
    /// a chaque mouvement de transaction. Aucune decision metier ne s'appuie
    /// sur ces chiffres.
    /// </remarks>
    [HttpGet("vendor")]
    [Authorize(Roles = "Vendor")]
    public async Task<IActionResult> Vendor(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetVendorDashboardQuery(UserId), ct);
        return Ok(new { data = result });
    }

    /// <summary>Tableau de bord de l'acheteur connecte.</summary>
    /// <remarks>
    /// Montant depense, fonds en sequestre, remboursements, commandes actives
    /// et notifications non lues. Meme politique de cache que le tableau de
    /// bord vendeur.
    /// </remarks>
    [HttpGet("buyer")]
    [Authorize(Roles = "Buyer")]
    public async Task<IActionResult> Buyer(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetBuyerDashboardQuery(UserId), ct);
        return Ok(new { data = result });
    }

    /// <summary>Tableau de bord de la plateforme.</summary>
    /// <remarks>
    /// Volume, sequestre, commission, taux de reussite, files d'attente et
    /// dernieres transactions.
    /// </remarks>
    /// <param name="range">Granularite de la courbe de volume : `7d` (defaut), `30d` ou `12m`.</param>
    [HttpGet("admin")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Admin([FromQuery] string range = "7d", CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetAdminDashboardQuery(range), ct);
        return Ok(new { data = result });
    }
}
