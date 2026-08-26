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

    /// <summary>Depose un dossier de verification d'identite.</summary>
    /// <remarks>
    /// Reserve aux vendeurs : c'est ce dossier qui ouvre le droit de creer des
    /// transactions. Un seul dossier peut etre en attente a la fois.
    ///
    /// Envoi `multipart/form-data` : `documentType` (`cin` ou `passport`),
    /// `documentFront` et `selfie` (JPG, PNG ou PDF, 5 Mo au plus).
    ///
    /// Les pieces ne sont jamais servies en statique : seul un administrateur
    /// peut les relire, par les endpoints dedies.
    /// </remarks>
    /// <response code="403">Le compte n'est pas un compte vendeur.</response>
    /// <response code="422">Format ou taille de fichier refuse, ou dossier deja en attente.</response>
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

    /// <summary>Etat du dossier d'identite de l'utilisateur connecte.</summary>
    /// <remarks>
    /// Rend `{ status, submittedAt }`, ou `status` vaut `not_submitted`,
    /// `pending`, `approved` ou `rejected`.
    /// </remarks>
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