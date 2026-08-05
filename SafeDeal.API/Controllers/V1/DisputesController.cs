using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SafeDeal.Application.Disputes.Commands.OpenDispute;
using SafeDeal.Application.Disputes.Commands.SubmitEvidence;
using SafeDeal.Application.Disputes.Queries.GetDispute;
using System.Security.Claims;

namespace SafeDeal.API.Controllers.V1;

[ApiController]
[Route("api/v1/transactions/{id:int}/dispute")]
[Authorize]
public class DisputesController : ControllerBase
{
    private readonly IMediator _mediator;
    public DisputesController(IMediator mediator) => _mediator = mediator;

    private int UserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpPost]
    public async Task<IActionResult> Open(int id, [FromBody] OpenDisputeRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(
            new OpenDisputeCommand(id, UserId, request.Category, request.Description, request.Files ?? []), ct);
        return Ok(new { message = "Litige ouvert avec succès.", data = result });
    }

    [HttpGet]
    public async Task<IActionResult> Get(int id, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetDisputeQuery(id, UserId), ct);
        return Ok(new { data = result });
    }

    [HttpPost("evidence")]
    public async Task<IActionResult> SubmitEvidence(int id, [FromBody] EvidenceRequest request, CancellationToken ct)
    {
        await _mediator.Send(new SubmitEvidenceCommand(id, UserId, request.Description, request.Files ?? []), ct);
        return Ok(new { message = "Preuve soumise avec succès." });
    }
}

public record OpenDisputeRequest(string Category, string Description, IEnumerable<string>? Files);
public record EvidenceRequest(string Description, IEnumerable<string>? Files);