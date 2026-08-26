using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SafeDeal.Application.Auth.Commands.ChangePassword;
using SafeDeal.Application.Auth.Commands.ForgotPassword;
using SafeDeal.Application.Auth.Commands.Login;
using SafeDeal.Application.Auth.Commands.Logout;
using SafeDeal.Application.Auth.Commands.RefreshToken;
using SafeDeal.Application.Auth.Commands.Register;
using SafeDeal.Application.Auth.Commands.ResendVerification;
using SafeDeal.Application.Auth.Commands.SendTwoFactor;
using SafeDeal.Application.Auth.Commands.SetTwoFactor;
using SafeDeal.Application.Auth.Commands.UpdateProfile;
using SafeDeal.Application.Auth.Commands.UploadAvatar;
using SafeDeal.Application.Auth.Commands.VerifyEmail;
using SafeDeal.Application.Auth.Commands.VerifyTwoFactor;
using SafeDeal.Application.Auth.Commands.ResetPassword;
using SafeDeal.Application.Auth.Queries.GetCurrentUser;
using System.Security.Claims;

namespace SafeDeal.API.Controllers.V1;

[ApiController]
[Route("api/v1")]
public class AuthController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IWebHostEnvironment _env;

    public AuthController(IMediator mediator, IWebHostEnvironment env)
    {
        _mediator = mediator;
        _env = env;
    }

    private int UserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private string Token => Request.Headers.Authorization.ToString().Replace("Bearer ", "");

    /// <summary>Cree un compte acheteur ou vendeur.</summary>
    /// <remarks>
    /// Le compte est actif immediatement, mais son e-mail n'est pas encore
    /// verifie : un code part par courriel et la connexion restera refusee tant
    /// qu'il n'est pas saisi sur POST /api/v1/auth/email/verify.
    ///
    /// Reponse : `{ token, refreshToken, user }`.
    /// </remarks>
    /// <response code="200">Compte cree, jeton delivre.</response>
    [HttpPost("register")]
    [EnableRateLimiting("register")]
    public async Task<IActionResult> Register([FromBody] RegisterCommand command, CancellationToken ct)
    {
        var result = await _mediator.Send(command, ct);
        return Ok(result);
    }

    /// <summary>Ouvre une session.</summary>
    /// <remarks>
    /// Trois issues :
    ///
    /// - identifiants valides : `{ token, refreshToken, user }` ;
    /// - 2FA active : `{ requiresTwoFactor: true }` sans jeton, le code part par
    ///   courriel et la session se termine sur POST /api/v1/verify-2fa ;
    /// - e-mail non verifie : 403 avec `email_verified: false`, pour que le
    ///   client redirige vers l'ecran de verification plutot que d'afficher une
    ///   erreur d'identifiants.
    /// </remarks>
    /// <response code="200">Session ouverte, ou seconde etape requise.</response>
    /// <response code="401">Identifiants invalides.</response>
    /// <response code="403">E-mail non verifie (`email_verified: false`).</response>
    [HttpPost("login")]
    [EnableRateLimiting("login")]
    public async Task<IActionResult> Login([FromBody] LoginCommand command, CancellationToken ct)
    {
        var result = await _mediator.Send(command, ct);
        return Ok(result);
    }

    /// <summary>Ferme la session et revoque le jeton presente.</summary>
    /// <remarks>
    /// Le jeton reste cryptographiquement valide jusqu'a son echeance : il est
    /// donc inscrit sur une liste noire dans Redis, ce qui le neutralise
    /// immediatement sur toute l'API.
    /// </remarks>
    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        await _mediator.Send(new LogoutCommand(Token), ct);
        return Ok(new { message = "Logged out successfully." });
    }

    /// <summary>Echange un jeton de rafraichissement contre une nouvelle session.</summary>
    /// <remarks>
    /// Volontairement accessible sans jeton d'acces : c'est precisement quand
    /// celui-ci a expire qu'on appelle ici. Le jeton de rafraichissement ne sert
    /// qu'une fois et est remplace a chaque appel.
    /// </remarks>
    /// <response code="200">Nouvelle paire de jetons.</response>
    /// <response code="401">Jeton de rafraichissement inconnu, expire ou deja consomme.</response>
    [HttpPost("auth/refresh")]
    [EnableRateLimiting("refresh")]
    public async Task<IActionResult> Refresh([FromBody] RefreshTokenCommand command, CancellationToken ct)
    {
        var result = await _mediator.Send(command, ct);
        return Ok(result);
    }

    /// <summary>Profil de l'utilisateur connecte.</summary>
    /// <remarks>Reponse : `{ user }`, avec le role, le statut d'identite et l'etat de la 2FA.</remarks>
    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> Me(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetCurrentUserQuery(UserId), ct);
        return Ok(new { user = result });
    }

    /// <summary>Met a jour le nom ou le telephone.</summary>
    /// <remarks>Les champs absents ou vides sont laisses inchanges.</remarks>
    [HttpPatch("me")]
    [Authorize]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest request, CancellationToken ct)
    {
        await _mediator.Send(new UpdateProfileCommand(UserId, request.Name, request.Phone), ct);
        return Ok(new { message = "Profile updated successfully." });
    }

    /// <summary>Change le mot de passe de l'utilisateur connecte.</summary>
    /// <remarks>
    /// Le mot de passe actuel est exige, et le nouveau doit differer : une
    /// rotation qui ne change rien laisse croire a une rotation effectuee.
    /// </remarks>
    /// <response code="401">Mot de passe actuel incorrect.</response>
    [HttpPost("me/change-password")]
    [Authorize]
    [EnableRateLimiting("mutations")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request, CancellationToken ct)
    {
        await _mediator.Send(new ChangePasswordCommand(UserId, request.CurrentPassword, request.NewPassword), ct);
        return Ok(new { message = "Password changed successfully." });
    }

    /// <summary>Rend l'avatar de l'utilisateur connecte.</summary>
    /// <remarks>Renvoie l'image binaire (`image/png` ou `image/jpeg`), pas du JSON.</remarks>
    /// <response code="404">Aucun avatar, ou fichier absent du disque.</response>
    [HttpGet("me/avatar")]
    [Authorize]
    public async Task<IActionResult> GetAvatar(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetCurrentUserQuery(UserId), ct);
        if (result.AvatarPath is null)
            return NotFound(new { message = "No avatar found." });

        var fullPath = Path.Combine(_env.ContentRootPath, result.AvatarPath);
        if (!System.IO.File.Exists(fullPath))
            return NotFound(new { message = "Avatar file not found." });

        var bytes = await System.IO.File.ReadAllBytesAsync(fullPath, ct);
        var ext = Path.GetExtension(fullPath).ToLower();
        var contentType = ext == ".png" ? "image/png" : "image/jpeg";
        return File(bytes, contentType);
    }

    /// <summary>Depose un avatar.</summary>
    /// <remarks>Envoi `multipart/form-data`, champ `file`. Formats image uniquement.</remarks>
    /// <param name="file">Image a enregistrer.</param>
    [HttpPost("me/avatar")]
    [Authorize]
    [EnableRateLimiting("mutations")]
    public async Task<IActionResult> UploadAvatar(IFormFile file, CancellationToken ct)
    {
        var uploadPath = Path.Combine(_env.ContentRootPath, "uploads");
        var path = await _mediator.Send(new UploadAvatarCommand(UserId, file, uploadPath), ct);
        return Ok(new { message = "Avatar uploaded successfully.", path });
    }

    /// <summary>Valide l'adresse e-mail avec le code recu.</summary>
    /// <remarks>Tant que l'e-mail n'est pas verifie, la connexion est refusee en 403.</remarks>
    /// <response code="422">Code invalide ou expire.</response>
    [HttpPost("auth/email/verify")]
    [Authorize]
    [EnableRateLimiting("verify-otp")]
    public async Task<IActionResult> VerifyEmail([FromBody] VerifyEmailRequest request, CancellationToken ct)
    {
        await _mediator.Send(new VerifyEmailCommand(UserId, request.Code), ct);
        return Ok(new { message = "Email verified successfully." });
    }

    /// <summary>Renvoie le code de verification d'adresse e-mail.</summary>
    [HttpPost("auth/email/resend")]
    [Authorize]
    [EnableRateLimiting("email-verification")]
    public async Task<IActionResult> ResendVerification(CancellationToken ct)
    {
        await _mediator.Send(new ResendVerificationCommand(UserId), ct);
        return Ok(new { message = "Verification code sent." });
    }

    /// <summary>Envoie un code de double authentification par courriel.</summary>
    [HttpPost("auth/2fa/send")]
    [Authorize]
    [EnableRateLimiting("otp")]
    public async Task<IActionResult> SendTwoFactor(CancellationToken ct)
    {
        await _mediator.Send(new SendTwoFactorCommand(UserId), ct);
        return Ok(new { message = "OTP sent." });
    }

    /// <summary>Seconde etape de la connexion : validation du code 2FA.</summary>
    /// <remarks>
    /// Sans jeton, par construction : l'utilisateur n'en a pas encore recu.
    /// Le code ne sert qu'une fois. Reponse identique a celle d'une connexion
    /// reussie : `{ token, refreshToken, user }`.
    /// </remarks>
    /// <response code="422">Code invalide, expire, ou deja consomme.</response>
    [HttpPost("verify-2fa")]
    [EnableRateLimiting("verify-otp")]
    public async Task<IActionResult> VerifyTwoFactor([FromBody] VerifyTwoFactorCommand command, CancellationToken ct)
    {
        var result = await _mediator.Send(command, ct);
        return Ok(result);
    }

    /// <summary>Active ou desactive la double authentification.</summary>
    /// <remarks>Reponse : `{ message, enabled }`.</remarks>
    [HttpPost("me/two-factor")]
    [Authorize]
    [EnableRateLimiting("mutations")]
    public async Task<IActionResult> SetTwoFactor([FromBody] SetTwoFactorRequest request, CancellationToken ct)
    {
        await _mediator.Send(new SetTwoFactorCommand(UserId, request.Enabled), ct);
        return Ok(new
        {
            message = request.Enabled
                ? "Two-factor authentication enabled."
                : "Two-factor authentication disabled.",
            enabled = request.Enabled
        });
    }

    /// <summary>Demande un lien de reinitialisation de mot de passe.</summary>
    /// <remarks>
    /// Repond toujours la meme chose, que l'adresse existe ou non : distinguer
    /// les deux cas revelerait quels comptes existent.
    /// </remarks>
    [HttpPost("forgot-password")]
    [EnableRateLimiting("password-reset")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordCommand command, CancellationToken ct)
    {
        await _mediator.Send(command, ct);
        return Ok(new { message = "If this email exists, a reset link has been sent." });
    }

    /// <summary>Fixe un nouveau mot de passe a partir du jeton recu par courriel.</summary>
    /// <response code="422">Jeton de reinitialisation invalide ou expire.</response>
    [HttpPost("reset-password")]
    [EnableRateLimiting("password-reset")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordCommand command, CancellationToken ct)
    {
        await _mediator.Send(command, ct);
        return Ok(new { message = "Password reset successfully." });
    }
}

public record VerifyEmailRequest(string Code);
public record SetTwoFactorRequest(bool Enabled);
public record UpdateProfileRequest(string? Name, string? Phone);
public record ChangePasswordRequest(string CurrentPassword, string NewPassword);


