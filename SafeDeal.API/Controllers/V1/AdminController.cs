using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SafeDeal.Application.Disputes.Commands.ResolveDispute;

namespace SafeDeal.API.Controllers.V1;

[ApiController]
[Route("api/v1/admin")]
[Authorize(Roles = "Admin")]
public class AdminController : ControllerBase
{
    private readonly IMediator _mediator;
    public AdminController(IMediator mediator) => _mediator = mediator;

    [HttpPost("disputes/{id:int}/resolve")]
    public async Task<IActionResult> ResolveDispute(int id, [FromBody] ResolveDisputeRequest request, CancellationToken ct)
    {
        await _mediator.Send(new ResolveDisputeCommand(id, request.Decision, request.Note), ct);
        return Ok(new { message = "Dispute resolved." });
    }
}

public record ResolveDisputeRequest(string Decision, string Note);