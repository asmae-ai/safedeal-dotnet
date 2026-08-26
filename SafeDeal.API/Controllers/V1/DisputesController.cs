using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SafeDeal.Application.Disputes.Commands.OpenDispute;
using SafeDeal.Application.Disputes.Commands.SubmitEvidence;
using SafeDeal.Application.Disputes.Queries.GetDispute;
using SafeDeal.Domain.Interfaces.Services;
using System.Security.Claims;

namespace SafeDeal.API.Controllers.V1;

[ApiController]
[Route("api/v1/transactions/{id:int}/dispute")]
[Authorize]
public class DisputesController : ControllerBase
{
    private static readonly string[] AllowedExtensions = [".jpg", ".jpeg", ".png", ".pdf"];
    private const long MaxFileSize = 5 * 1024 * 1024;
    private const int MaxFiles = 4;

    private readonly IMediator _mediator;
    private readonly IFileStorageService _fileStorage;
    private readonly IWebHostEnvironment _env;

    public DisputesController(IMediator mediator, IFileStorageService fileStorage, IWebHostEnvironment env)
    {
        _mediator = mediator;
        _fileStorage = fileStorage;
        _env = env;
    }

    private int UserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    /// <summary>Ouvre un litige sur une transaction.</summary>
    /// <remarks>
    /// Reserve aux deux parties. L'ouverture gele la transaction : les fonds ne
    /// peuvent plus etre liberes tant qu'un administrateur n'a pas tranche.
    /// Une transaction ne porte qu'un seul litige.
    ///
    /// Envoi `multipart/form-data` : `category`, `description`, et jusqu'a
    /// quatre fichiers `files` (JPG, PNG ou PDF, 5 Mo chacun au plus).
    /// </remarks>
    /// <response code="403">L'appelant n'est pas partie a la transaction.</response>
    /// <response code="422">Litige deja ouvert, piece jointe refusee, ou transition impossible.</response>
    [HttpPost]
    [EnableRateLimiting("mutations")]
    public async Task<IActionResult> Open(int id, [FromForm] OpenDisputeRequest request, CancellationToken ct)
    {
        var (paths, error) = await StoreEvidenceAsync(request.Files, ct);
        if (error is not null) return UnprocessableEntity(new { message = error });

        var result = await _mediator.Send(
            new OpenDisputeCommand(id, UserId, request.Category, request.Description, paths), ct);

        return Ok(new { message = "Litige ouvert avec succès.", data = result });
    }

    /// <summary>Lit le litige d'une transaction, avec ses echanges.</summary>
    /// <remarks>Reserve aux deux parties : un tiers ne voit rien du dossier.</remarks>
    /// <response code="403">L'appelant n'est pas partie a la transaction.</response>
    /// <response code="404">Aucun litige sur cette transaction.</response>
    [HttpGet]
    public async Task<IActionResult> Get(int id, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetDisputeQuery(id, UserId), ct);
        return Ok(new { data = result });
    }

    /// <summary>Verse une reponse et ses pieces au dossier du litige.</summary>
    /// <remarks>
    /// Envoi `multipart/form-data` : `description` (10 caracteres au moins) et
    /// jusqu'a quatre fichiers `files`. Un dossier deja tranche n'accepte plus
    /// d'echange.
    /// </remarks>
    /// <response code="403">L'appelant n'est pas partie a la transaction.</response>
    /// <response code="422">Description trop courte, piece refusee, ou litige deja tranche.</response>
    [HttpPost("evidence")]
    [EnableRateLimiting("mutations")]
    public async Task<IActionResult> SubmitEvidence(int id, [FromForm] EvidenceRequest request, CancellationToken ct)
    {
        var (paths, error) = await StoreEvidenceAsync(request.Files, ct);
        if (error is not null) return UnprocessableEntity(new { message = error });

        await _mediator.Send(new SubmitEvidenceCommand(id, UserId, request.Description, paths), ct);
        return Ok(new { message = "Preuve soumise avec succès." });
    }

    /// <summary>Sert une piece jointe du litige.</summary>
    /// <remarks>
    /// L'appartenance a la transaction est verifiee avant tout acces disque : le
    /// dossier des pieces n'est pas expose en statique.
    /// </remarks>
    /// <param name="fileName">Nom du fichier, tel qu'il figure dans le dossier.</param>
    /// <response code="403">L'appelant n'est pas partie a la transaction.</response>
    /// <response code="404">Piece inconnue de ce litige, ou absente du disque.</response>
    [HttpGet("evidence/{fileName}")]
    public async Task<IActionResult> GetEvidence(int id, string fileName, CancellationToken ct)
    {
        // Verifie l'appartenance a la transaction avant tout acces disque.
        var dispute = await _mediator.Send(new GetDisputeQuery(id, UserId), ct);

        var known = dispute.Evidences.SelectMany(e => e.Files)
            .Any(f => Path.GetFileName(f) == fileName);
        if (!known)
            return NotFound(new { message = "Evidence not found for this dispute." });

        // Path.GetFileName neutralise toute tentative de remontee de repertoire.
        var safeName = Path.GetFileName(fileName);
        var fullPath = Path.Combine(_env.ContentRootPath, "uploads", "disputes", safeName);
        if (!System.IO.File.Exists(fullPath))
            return NotFound(new { message = "File not found on disk." });

        var bytes = await System.IO.File.ReadAllBytesAsync(fullPath, ct);
        var ext = Path.GetExtension(safeName).ToLowerInvariant();
        var contentType = ext switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".pdf" => "application/pdf",
            _ => "application/octet-stream"
        };
        return File(bytes, contentType);
    }

    private async Task<(List<string> Paths, string? Error)> StoreEvidenceAsync(
        IFormFileCollection? files, CancellationToken ct)
    {
        var paths = new List<string>();
        if (files is null || files.Count == 0) return (paths, null);

        if (files.Count > MaxFiles)
            return (paths, $"You can attach at most {MaxFiles} files.");

        foreach (var file in files)
        {
            if (!_fileStorage.IsValidExtension(file.FileName, AllowedExtensions))
                return (paths, "Only JPG, PNG and PDF files are allowed.");

            if (!_fileStorage.IsValidSize(file.Length, MaxFileSize))
                return (paths, "Each file must be 5 MB or smaller.");
        }

        foreach (var file in files)
        {
            var path = await _fileStorage.SaveAsync(
                file.OpenReadStream(), file.FileName, "disputes", ct);
            paths.Add(path.Replace('\\', '/'));
        }

        return (paths, null);
    }
}

public record OpenDisputeRequest(string Category, string Description, IFormFileCollection? Files);
public record EvidenceRequest(string Description, IFormFileCollection? Files);
