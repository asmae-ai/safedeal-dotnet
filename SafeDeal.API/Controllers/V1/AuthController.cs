using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SafeDeal.Application.Auth.Commands.ChangePassword;
using SafeDeal.Application.Auth.Commands.ForgotPassword;
using SafeDeal.Application.Auth.Commands.Login;
using SafeDeal.Application.Auth.Commands.Logout;
using SafeDeal.Application.Auth.Commands.Register;
using SafeDeal.Application.Auth.Commands.ResendVerification;
using SafeDeal.Application.Auth.Commands.SendTwoFactor;
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

    [HttpPost("register")]
    [EnableRateLimiting("register")]
    public async Task<IActionResult> Register([FromBody] RegisterCommand command, CancellationToken ct)
    {
        var result = await _mediator.Send(command, ct);
        return Ok(result);
    }

    [HttpPost("login")]
    [EnableRateLimiting("login")]
    public async Task<IActionResult> Login([FromBody] LoginCommand command, CancellationToken ct)
    {
        var result = await _mediator.Send(command, ct);
        return Ok(result);
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        await _mediator.Send(new LogoutCommand(Token), ct);
        return Ok(new { message = "Logged out successfully." });
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> Me(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetCurrentUserQuery(UserId), ct);
        return Ok(new { user = result });
    }

    [HttpPatch("me")]
    [Authorize]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest request, CancellationToken ct)
    {
        await _mediator.Send(new UpdateProfileCommand(UserId, request.Name, request.Phone), ct);
        return Ok(new { message = "Profile updated successfully." });
    }

    [HttpPost("me/change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request, CancellationToken ct)
    {
        await _mediator.Send(new ChangePasswordCommand(UserId, request.CurrentPassword, request.NewPassword), ct);
        return Ok(new { message = "Password changed successfully." });
    }

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

    [HttpPost("me/avatar")]
    [Authorize]
    public async Task<IActionResult> UploadAvatar(IFormFile file, CancellationToken ct)
    {
        var uploadPath = Path.Combine(_env.ContentRootPath, "uploads");
        var path = await _mediator.Send(new UploadAvatarCommand(UserId, file, uploadPath), ct);
        return Ok(new { message = "Avatar uploaded successfully.", path });
    }

    [HttpPost("auth/email/verify")]
    [Authorize]
    public async Task<IActionResult> VerifyEmail([FromBody] VerifyEmailRequest request, CancellationToken ct)
    {
        await _mediator.Send(new VerifyEmailCommand(UserId, request.Code), ct);
        return Ok(new { message = "Email verified successfully." });
    }

    [HttpPost("auth/email/resend")]
    [Authorize]
    public async Task<IActionResult> ResendVerification(CancellationToken ct)
    {
        await _mediator.Send(new ResendVerificationCommand(UserId), ct);
        return Ok(new { message = "Verification code sent." });
    }

    [HttpPost("auth/2fa/send")]
    [Authorize]
    [EnableRateLimiting("otp")]
    public async Task<IActionResult> SendTwoFactor(CancellationToken ct)
    {
        await _mediator.Send(new SendTwoFactorCommand(UserId), ct);
        return Ok(new { message = "OTP sent." });
    }

    [HttpPost("verify-2fa")]
    [Authorize]
    public async Task<IActionResult> VerifyTwoFactor([FromBody] VerifyOtpRequest request, CancellationToken ct)
    {
        await _mediator.Send(new VerifyTwoFactorCommand(UserId, request.Code), ct);
        return Ok(new { message = "2FA verified." });
    }

    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordCommand command, CancellationToken ct)
    {
        await _mediator.Send(command, ct);
        return Ok(new { message = "If this email exists, a reset link has been sent." });
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordCommand command, CancellationToken ct)
    {
        await _mediator.Send(command, ct);
        return Ok(new { message = "Password reset successfully." });
    }
}

public record VerifyEmailRequest(string Code);
public record VerifyOtpRequest(string Code);
public record UpdateProfileRequest(string? Name, string? Phone);
public record ChangePasswordRequest(string CurrentPassword, string NewPassword);


