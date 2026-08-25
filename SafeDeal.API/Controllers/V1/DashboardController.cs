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

    [HttpGet("vendor")]
    [Authorize(Roles = "Vendor")]
    public async Task<IActionResult> Vendor(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetVendorDashboardQuery(UserId), ct);
        return Ok(new { data = result });
    }

    [HttpGet("buyer")]
    [Authorize(Roles = "Buyer")]
    public async Task<IActionResult> Buyer(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetBuyerDashboardQuery(UserId), ct);
        return Ok(new { data = result });
    }

    [HttpGet("admin")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Admin([FromQuery] string range = "7d", CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetAdminDashboardQuery(range), ct);
        return Ok(new { data = result });
    }
}
