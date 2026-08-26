using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SafeDeal.Application.Notifications.Commands.MarkAllRead;
using SafeDeal.Application.Notifications.Commands.MarkOneRead;
using SafeDeal.Application.Notifications.Queries.GetNotifications;
using System.Security.Claims;

namespace SafeDeal.API.Controllers.V1;

[ApiController]
[Route("api/v1/notifications")]
[Authorize]
public class NotificationsController : ControllerBase
{
    private readonly IMediator _mediator;
    public NotificationsController(IMediator mediator) => _mediator = mediator;

    private int UserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    /// <param name="page">Optionnel. Absent, la liste complete est rendue comme avant.</param>
    /// <param name="perPage">Optionnel, 20 par defaut, plafonne a 100.</param>
    /// <summary>Notifications de l'utilisateur connecte, de la plus recente a la plus ancienne.</summary>
    /// <remarks>
    /// Sans parametre `page`, la liste complete est rendue : `{ data }`.
    /// Des qu'une page est demandee, la reponse porte en plus
    /// `meta: { current_page, last_page, total }`.
    /// </remarks>
    [HttpGet]
    public async Task<IActionResult> GetNotifications(
        [FromQuery] int? page = null,
        [FromQuery(Name = "per_page")] int perPage = 20,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetNotificationsQuery(UserId, page, perPage), ct);

        if (!page.HasValue) return Ok(new { data = result.Data });

        return Ok(new
        {
            data = result.Data,
            meta = new { current_page = result.CurrentPage, last_page = result.LastPage, total = result.Total }
        });
    }

    /// <summary>Marque toutes les notifications comme lues.</summary>
    [HttpPost("read-all")]
    public async Task<IActionResult> MarkAllRead(CancellationToken ct)
    {
        await _mediator.Send(new MarkAllReadCommand(UserId), ct);
        return Ok(new { message = "All notifications marked as read." });
    }

    /// <summary>Marque une notification comme lue.</summary>
    /// <remarks>
    /// Une notification appartenant a un autre compte sort en 404, jamais en
    /// 403 : repondre « interdit » confirmerait son existence.
    /// </remarks>
    /// <response code="404">Notification inexistante, ou appartenant a un autre compte.</response>
    [HttpPost("{id:int}/read")]
    public async Task<IActionResult> MarkOneRead(int id, CancellationToken ct)
    {
        await _mediator.Send(new MarkOneReadCommand(UserId, id), ct);
        return Ok(new { message = "Notification marked as read." });
    }
}
