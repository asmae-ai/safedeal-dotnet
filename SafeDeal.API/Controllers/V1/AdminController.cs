using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SafeDeal.Application.Admin.Commands.ApproveIdentity;
using SafeDeal.Application.Admin.Commands.RejectIdentity;
using SafeDeal.Application.Admin.Queries.GetAllDisputes;
using SafeDeal.Application.Admin.Queries.GetAllTransactions;
using SafeDeal.Application.Admin.Queries.GetAllUsers;
using SafeDeal.Application.Admin.Queries.GetAuditLogs;
using SafeDeal.Application.Admin.Queries.GetDisputeStats;
using SafeDeal.Application.Admin.Queries.GetIdentityStats;
using SafeDeal.Application.Admin.Queries.GetPendingVerifications;
using SafeDeal.Application.Admin.Queries.GetStatistics;
using SafeDeal.Application.Common.Models;
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

    /// <summary>
    /// Enveloppe commune des listes paginees. Le contrat historique
    /// (<c>data</c> + <c>meta.current_page</c>/<c>last_page</c>/<c>total</c>)
    /// est conserve tel quel.
    /// </summary>
    private static object Paged<T>(PagedResult<T> result) => new
    {
        data = result.Data,
        meta = new { current_page = result.CurrentPage, last_page = result.LastPage, total = result.Total }
    };

    /// <summary>Compteurs de la plateforme.</summary>
    /// <remarks>
    /// Utilisateurs par role, transactions par etat, litiges, dossiers
    /// d'identite en attente. Servis depuis le cache, avec une duree de vie
    /// courte : ce sont des indicateurs, pas une source de verite comptable.
    /// </remarks>
    [HttpGet("stats")]
    public async Task<IActionResult> GetStats(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetStatisticsQuery(), ct);
        return Ok(new { data = result });
    }

    /// <param name="perPage">Optionnel, 20 par defaut, plafonne a 100.</param>
    /// <summary>Liste les comptes, du plus recent au plus ancien.</summary>
    /// <remarks>
    /// Recherche et filtre s'appliquent en base, pas sur la page deja chargee :
    /// un compte des pages suivantes reste trouvable.
    ///
    /// Reponse : `{ data, meta: { current_page, last_page, total } }`.
    /// </remarks>
    /// <param name="page">Page demandee, 1 par defaut.</param>
    /// <param name="search">Fragment de nom ou d'adresse e-mail.</param>
    /// <param name="role">`vendor`, `buyer` ou `admin`.</param>
    [HttpGet("users")]
    public async Task<IActionResult> GetUsers(
        [FromQuery] int page = 1,
        [FromQuery] string? search = null,
        [FromQuery] string? role = null,
        [FromQuery(Name = "per_page")] int perPage = 20,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetAllUsersQuery(page, perPage, search, role), ct);
        return Ok(Paged(result));
    }

    /// <param name="page">Optionnel. Absent, la file complete est rendue comme avant.</param>
    /// <summary>File des dossiers d'identite en attente d'examen, du plus ancien au plus recent.</summary>
    /// <remarks>
    /// L'ordre est volontairement croissant : personne ne doit rester au fond de
    /// la file parce que de nouveaux dossiers arrivent.
    ///
    /// Sans parametre `page`, la file complete est rendue : `{ data }`.
    /// </remarks>
    [HttpGet("identities")]
    public async Task<IActionResult> GetPendingIdentities(
        [FromQuery] int? page = null,
        [FromQuery(Name = "per_page")] int perPage = 20,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetPendingVerificationsQuery(page, perPage), ct);
        return Ok(page.HasValue ? Paged(result) : new { data = result.Data });
    }

    /// <summary>Compteurs de la file des verifications d'identite.</summary>
    /// <remarks>Total, en attente, approuves, refuses, entrees du mois, taux de traitement.</remarks>
    [HttpGet("identities/stats")]
    public async Task<IActionResult> GetIdentityStats(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetIdentityStatsQuery(), ct);
        return Ok(new { data = result });
    }

    /// <summary>Compteurs de la file des litiges.</summary>
    /// <remarks>
    /// Meme forme que les compteurs d'identite, avec des libelles propres a
    /// l'ecran : ouverts, en cours d'examen, tranches.
    /// </remarks>
    [HttpGet("disputes/stats")]
    public async Task<IActionResult> GetDisputeStats(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetDisputeStatsQuery(), ct);
        return Ok(new { data = result });
    }

    /// <summary>Approuve le dossier d'identite d'un vendeur.</summary>
    /// <remarks>
    /// Ouvre au compte le droit de creer des transactions. La decision est
    /// tracee au journal d'audit avec son auteur.
    /// </remarks>
    /// <param name="userId">Compte dont le dossier est examine.</param>
    /// <response code="404">Aucun dossier pour ce compte.</response>
    [HttpPost("identities/{userId:int}/approve")]
    public async Task<IActionResult> ApproveIdentity(int userId, CancellationToken ct)
    {
        await _mediator.Send(new ApproveIdentityCommand(userId), ct);
        return Ok(new { message = "Identity approved successfully." });
    }

    /// <summary>Refuse le dossier d'identite d'un vendeur.</summary>
    /// <remarks>
    /// Le motif est obligatoire : il est rendu a l'interesse pour qu'il sache
    /// quoi corriger. Un refus n'est pas definitif — un nouveau depot peut etre
    /// approuve.
    /// </remarks>
    /// <param name="userId">Compte dont le dossier est examine.</param>
    /// <response code="404">Aucun dossier pour ce compte.</response>
    [HttpPost("identities/{userId:int}/reject")]
    public async Task<IActionResult> RejectIdentity(int userId, [FromBody] RejectIdentityRequest request, CancellationToken ct)
    {
        await _mediator.Send(new RejectIdentityCommand(userId, request.Reason), ct);
        return Ok(new { message = "Identity rejected." });
    }

    /// <summary>Liste toutes les transactions de la plateforme.</summary>
    /// <remarks>Reponse : `{ data, meta: { current_page, last_page, total } }`.</remarks>
    /// <param name="page">Page demandee, 1 par defaut.</param>
    /// <param name="search">Fragment de titre, de nom de vendeur ou d'acheteur.</param>
    /// <param name="status">Etat exact, en `snake_case` (`payment_received`, `in_shipping`...).</param>
    [HttpGet("transactions")]
    public async Task<IActionResult> GetAllTransactions(
        [FromQuery] int page = 1,
        [FromQuery] string? search = null,
        [FromQuery] string? status = null,
        [FromQuery(Name = "per_page")] int perPage = 20,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetAllTransactionsQuery(page, perPage, search, status), ct);
        return Ok(Paged(result));
    }

    /// <param name="page">Optionnel. Absent, la liste complete est rendue comme avant.</param>
    /// <summary>Liste les litiges de la plateforme, du plus recent au plus ancien.</summary>
    /// <remarks>Sans parametre `page`, la liste complete est rendue : `{ data }`.</remarks>
    /// <param name="status">`open` (defaut), `settled` ou `all`.</param>
    [HttpGet("disputes")]
    public async Task<IActionResult> GetAllDisputes(
        [FromQuery] string status = "open",
        [FromQuery] int? page = null,
        [FromQuery(Name = "per_page")] int perPage = 20,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetAllDisputesQuery(status, page, perPage), ct);
        return Ok(page.HasValue ? Paged(result) : new { data = result.Data });
    }

    /// <summary>Tranche un litige.</summary>
    /// <remarks>
    /// `decision` vaut `resolved` (en faveur du vendeur, les fonds lui reviennent)
    /// ou `refunded` (en faveur de l'acheteur, remboursement emis).
    ///
    /// Un remboursement est demande au prestataire **avant** d'ecrire le statut :
    /// s'il refuse, le litige reste ouvert plutot que d'afficher un
    /// remboursement qui n'a pas eu lieu. Un litige deja tranche ne peut pas
    /// l'etre une seconde fois.
    /// </remarks>
    /// <param name="id">Identifiant du litige.</param>
    /// <response code="404">Litige inconnu.</response>
    /// <response code="422">Decision inconnue, litige deja tranche, ou remboursement refuse.</response>
    [HttpPost("disputes/{id:int}/resolve")]
    public async Task<IActionResult> ResolveDispute(int id, [FromBody] ResolveDisputeRequest request, CancellationToken ct)
    {
        await _mediator.Send(new ResolveDisputeCommand(id, request.Decision, request.Note), ct);
        return Ok(new { message = "Dispute resolved." });
    }

    /// <summary>
    /// Journal d'audit, du plus recent au plus ancien. Toujours pagine : la
    /// table ne fait que croitre.
    /// </summary>
    /// <param name="action">Nom d'action exact ("Login", "TransactionShipped"...).</param>
    /// <param name="succeeded">Vrai pour les succes, faux pour les echecs, absent pour les deux.</param>
    [HttpGet("audit-logs")]
    public async Task<IActionResult> GetAuditLogs(
        [FromQuery] int page = 1,
        [FromQuery(Name = "per_page")] int perPage = 20,
        [FromQuery] string? action = null,
        [FromQuery(Name = "user_id")] int? userId = null,
        [FromQuery(Name = "entity_type")] string? entityType = null,
        [FromQuery(Name = "entity_id")] int? entityId = null,
        [FromQuery] bool? succeeded = null,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(
            new GetAuditLogsQuery(page, perPage, action, userId, entityType, entityId, succeeded), ct);

        return Ok(Paged(result));
    }

    /// <summary>Sert le recto de la piece d'identite d'un dossier.</summary>
    /// <remarks>
    /// Renvoie l'image binaire. Ces pieces ne sont jamais exposees en statique :
    /// leur seul acces passe par cet endpoint, reserve aux administrateurs.
    /// </remarks>
    /// <param name="id">Identifiant du dossier de verification.</param>
    /// <response code="404">Dossier inconnu, ou fichier absent du disque.</response>
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

    /// <summary>Sert le selfie d'un dossier d'identite.</summary>
    /// <remarks>Meme protection que le recto de la piece : acces administrateur uniquement.</remarks>
    /// <param name="id">Identifiant du dossier de verification.</param>
    /// <response code="404">Dossier inconnu, ou fichier absent du disque.</response>
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