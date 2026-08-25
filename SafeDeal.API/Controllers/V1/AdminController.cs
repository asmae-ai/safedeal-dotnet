using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SafeDeal.Application.Admin.Commands.ApproveIdentity;
using SafeDeal.Application.Admin.Commands.RejectIdentity;
using SafeDeal.Application.Admin.Queries.GetAllDisputes;
using SafeDeal.Application.Admin.Queries.GetAllTransactions;
using SafeDeal.Application.Admin.Queries.GetAllUsers;
using SafeDeal.Application.Admin.Queries.GetDisputeStats;
using SafeDeal.Application.Admin.Queries.GetIdentityStats;
using SafeDeal.Application.Admin.Queries.GetPendingVerifications;
using SafeDeal.Application.Admin.Queries.GetStatistics;
using SafeDeal.Application.Disputes.Commands.ResolveDispute;
using SafeDeal.Domain.Interfaces.Repositories;

namespace SafeDeal.API.Controllers.V1;

[ApiController]
[Route("api/v1/admin")]
[Authorize(Roles = "Admin")]
public class AdminController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IIdentityVerificationRepository _verifications;
    private readonly IWebHostEnvironment _env;

    public AdminController(
        IMediator mediator,
        IIdentityVerificationRepository verifications,
        IWebHostEnvironment env)
    {
        _mediator = mediator;
        _verifications = verifications;
        _env = env;
    }

    [HttpGet("stats")]
    public async Task<IActionResult> GetStats(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetStatisticsQuery(), ct);
        return Ok(new { data = result });
    }

    [HttpGet("users")]
    public async Task<IActionResult> GetUsers(
        [FromQuery] int page = 1,
        [FromQuery] string? search = null,
        [FromQuery] string? role = null,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetAllUsersQuery(page, 20, search, role), ct);
        return Ok(new
        {
            data = result.Data,
            meta = new { current_page = result.CurrentPage, last_page = result.LastPage, total = result.Total }
        });
    }

    [HttpGet("identities")]
    public async Task<IActionResult> GetPendingIdentities(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetPendingVerificationsQuery(), ct);
        return Ok(new { data = result });
    }

    [HttpGet("identities/stats")]
    public async Task<IActionResult> GetIdentityStats(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetIdentityStatsQuery(), ct);
        return Ok(new { data = result });
    }

    [HttpGet("disputes/stats")]
    public async Task<IActionResult> GetDisputeStats(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetDisputeStatsQuery(), ct);
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
    public async Task<IActionResult> GetAllTransactions(
        [FromQuery] int page = 1,
        [FromQuery] string? search = null,
        [FromQuery] string? status = null,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetAllTransactionsQuery(page, 20, search, status), ct);
        return Ok(new
        {
            data = result.Data,
            meta = new { current_page = result.CurrentPage, last_page = result.LastPage, total = result.Total }
        });
    }

    [HttpGet("disputes")]
    public async Task<IActionResult> GetAllDisputes([FromQuery] string status = "open", CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetAllDisputesQuery(status), ct);
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
        var verification = await _verifications.GetByIdAsync(id, ct);
        if (verification is null)
            return NotFound(new { message = "Verification not found." });

        var fullPath = Path.Combine(_env.ContentRootPath, verification.DocumentFrontPath);
        if (!System.IO.File.Exists(fullPath))
            return NotFound(new { message = "Document file not found on disk." });

        var bytes = await System.IO.File.ReadAllBytesAsync(fullPath, ct);
        return File(bytes, GetContentType(fullPath));
    }

    [HttpGet("identities/{id:int}/document/selfie")]
    public async Task<IActionResult> GetSelfie(int id, CancellationToken ct)
    {
        var verification = await _verifications.GetByIdAsync(id, ct);
        if (verification is null)
            return NotFound(new { message = "Verification not found." });

        var fullPath = Path.Combine(_env.ContentRootPath, verification.SelfiePath);
        if (!System.IO.File.Exists(fullPath))
            return NotFound(new { message = "Selfie file not found on disk." });

        var bytes = await System.IO.File.ReadAllBytesAsync(fullPath, ct);
        return File(bytes, GetContentType(fullPath));
    }

    private static string GetContentType(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return ext switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".pdf" => "application/pdf",
            _ => "application/octet-stream"
        };
    }
}

public record RejectIdentityRequest(string Reason);
public record ResolveDisputeRequest(string Decision, string Note);