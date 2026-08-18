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

    [HttpGet]
    public async Task<IActionResult> GetNotifications(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetNotificationsQuery(UserId), ct);
        return Ok(new { data = result });
    }

    [HttpPost("read-all")]
    public async Task<IActionResult> MarkAllRead(CancellationToken ct)
    {
        await _mediator.Send(new MarkAllReadCommand(UserId), ct);
        return Ok(new { message = "All notifications marked as read." });
    }

    [HttpPost("{id:int}/read")]
    public async Task<IActionResult> MarkOneRead(int id, CancellationToken ct)
    {
        await _mediator.Send(new MarkOneReadCommand(UserId, id), ct);
        return Ok(new { message = "Notification marked as read." });
    }
}
