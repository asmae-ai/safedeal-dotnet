using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SafeDeal.Application.Admin.Commands.ApproveIdentity;
using SafeDeal.Application.Admin.Commands.RejectIdentity;
using SafeDeal.Application.Admin.Queries.GetAllDisputes;
using SafeDeal.Application.Admin.Queries.GetAllTransactions;
using SafeDeal.Application.Admin.Queries.GetAllUsers;
using SafeDeal.Application.Admin.Queries.GetPendingVerifications;
using SafeDeal.Application.Admin.Queries.GetStatistics;
using SafeDeal.Application.Disputes.Commands.ResolveDispute;

namespace SafeDeal.API.Controllers.V1;

[ApiController]
[Route("api/v1/admin")]
[Authorize(Roles = "Admin")]
public class AdminController : ControllerBase
{
    private readonly IMediator _mediator;
    public AdminController(IMediator mediator) => _mediator = mediator;

    [HttpGet("stats")]
    public async Task<IActionResult> GetStats(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetStatisticsQuery(), ct);
        return Ok(new { data = result });
    }

    [HttpGet("users")]
    public async Task<IActionResult> GetUsers([FromQuery] int page = 1, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetAllUsersQuery(page), ct);
        return Ok(new { data = result });
    }

    [HttpGet("identities")]
    public async Task<IActionResult> GetPendingIdentities(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetPendingVerificationsQuery(), ct);
        return Ok(new { data = result });
    }

    [HttpPost("identities/{userId:int}/approve")]
    public async Task<IActionResult> ApproveIdentity(int userId, CancellationToken ct)
    {
        await _mediator.Send(new ApproveIdentityCommand(userId), ct);
        return Ok(new { message = "Identity approved successfully." });
    }

    [HttpPost("identities/{userId:int}/reject")]
    public async Task<IActionResult> RejectIdentity(int userId, [FromBody] RejectIdentityRequest request, CancellationToken ct)
    {
        await _mediator.Send(new RejectIdentityCommand(userId, request.Reason), ct);
        return Ok(new { message = "Identity rejected." });
    }

    [HttpGet("transactions")]
    public async Task<IActionResult> GetAllTransactions([FromQuery] int page = 1, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetAllTransactionsQuery(page), ct);
        return Ok(new
        {
            data = result.Data,
            meta = new { current_page = result.CurrentPage, last_page = result.LastPage, total = result.Total }
        });
    }

    [HttpGet("disputes")]
    public async Task<IActionResult> GetAllDisputes(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetAllDisputesQuery(), ct);
        return Ok(new { data = result });
    }

    [HttpPost("disputes/{id:int}/resolve")]
    public async Task<IActionResult> ResolveDispute(int id, [FromBody] ResolveDisputeRequest request, CancellationToken ct)
    {
        await _mediator.Send(new ResolveDisputeCommand(id, request.Decision, request.Note), ct);
        return Ok(new { message = "Dispute resolved." });
    }

    [HttpGet("identities/{id:int}/document/front")]
    public async Task<IActionResult> GetDocumentFront(int id, CancellationToken ct)
    {
        var verifications = await _mediator.Send(new GetPendingVerificationsQuery(), ct);
        var doc = verifications.FirstOrDefault(v => v.Id == id);
        if (doc is null) return NotFound();
        if (!System.IO.File.Exists(doc.DocumentFrontPath)) return NotFound();
        var bytes = await System.IO.File.ReadAllBytesAsync(doc.DocumentFrontPath, ct);
        return File(bytes, "image/jpeg");
    }

    [HttpGet("identities/{id:int}/document/selfie")]
    public async Task<IActionResult> GetSelfie(int id, CancellationToken ct)
    {
        var verifications = await _mediator.Send(new GetPendingVerificationsQuery(), ct);
        var doc = verifications.FirstOrDefault(v => v.Id == id);
        if (doc is null) return NotFound();
        if (!System.IO.File.Exists(doc.SelfiePath)) return NotFound();
        var bytes = await System.IO.File.ReadAllBytesAsync(doc.SelfiePath, ct);
        return File(bytes, "image/jpeg");
    }
}

public record RejectIdentityRequest(string Reason);
public record ResolveDisputeRequest(string Decision, string Note);
