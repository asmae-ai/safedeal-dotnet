using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SafeDeal.Application.Notifications.Commands.MarkAllAsRead;
using SafeDeal.Application.Notifications.Commands.MarkAsRead;
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

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetNotificationsQuery(UserId), ct);
        return Ok(new { data = result });
    }

    [HttpPatch("{id:int}/read")]
    public async Task<IActionResult> MarkAsRead(int id, CancellationToken ct)
    {
        await _mediator.Send(new MarkAsReadCommand(id, UserId), ct);
        return Ok(new { message = "Marked as read." });
    }

    [HttpPatch("read-all")]
    public async Task<IActionResult> MarkAllAsRead(CancellationToken ct)
    {
        await _mediator.Send(new MarkAllAsReadCommand(UserId), ct);
        return Ok(new { message = "All notifications marked as read." });
    }
}