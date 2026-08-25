using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SafeDeal.Application.Identity.Commands.SubmitVerification;
using SafeDeal.Application.Identity.Queries.GetVerificationStatus;
using SafeDeal.Domain.Interfaces.Services;
using System.Security.Claims;

namespace SafeDeal.API.Controllers.V1;

[ApiController]
[Route("api/v1/verify-identity")]
[Authorize]
public class IdentityController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IFileStorageService _fileStorage;

    public IdentityController(IMediator mediator, IFileStorageService fileStorage)
    {
        _mediator = mediator;
        _fileStorage = fileStorage;
    }

    private int UserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpPost]
    [EnableRateLimiting("mutations")]
    public async Task<IActionResult> Submit([FromForm] SubmitVerificationRequest request, CancellationToken ct)
    {
        var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".pdf" };
        const long maxSize = 5 * 1024 * 1024;

        if (!_fileStorage.IsValidExtension(request.DocumentFront.FileName, allowedExtensions) ||
            !_fileStorage.IsValidExtension(request.Selfie.FileName, allowedExtensions))
            return UnprocessableEntity(new { message = "Invalid file type." });

        if (!_fileStorage.IsValidSize(request.DocumentFront.Length, maxSize) ||
            !_fileStorage.IsValidSize(request.Selfie.Length, maxSize))
            return UnprocessableEntity(new { message = "File size exceeds 5MB." });

        var frontPath = await _fileStorage.SaveAsync(
            request.DocumentFront.OpenReadStream(),
            request.DocumentFront.FileName,
            "identity", ct);

        var selfiePath = await _fileStorage.SaveAsync(
            request.Selfie.OpenReadStream(),
            request.Selfie.FileName,
            "identity", ct);

        await _mediator.Send(new SubmitVerificationCommand(
            UserId, request.DocumentType, frontPath, selfiePath), ct);

        return Ok(new { message = "Verification submitted." });
    }

    [HttpGet("status")]
    public async Task<IActionResult> Status(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetVerificationStatusQuery(UserId), ct);
        return Ok(result);
    }
}

public record SubmitVerificationRequest(
    string DocumentType,
    IFormFile DocumentFront,
    IFormFile Selfie);