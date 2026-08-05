using Microsoft.Extensions.Configuration;
using SafeDeal.Domain.Interfaces.Services;
using System.Net;
using System.Net.Mail;

namespace SafeDeal.Infrastructure.Services.Email;

public class EmailService : IEmailService
{
    private readonly IConfiguration _config;

    public EmailService(IConfiguration config) => _config = config;

    public async Task SendVerificationCodeAsync(string email, string name, string code, CancellationToken ct = default)
        => await SendAsync(email, "SafeDeal — Verify your email",
            $"Hi {name},\n\nYour verification code is: {code}\n\nExpires in 10 minutes.", ct);

    public async Task SendPasswordResetAsync(string email, string name, string token, CancellationToken ct = default)
    => await SendAsync(email, "SafeDeal — Reset your password",
        $"Hi {name},\n\nClick the link below to reset your password :\n\nhttp://localhost:5173/reset-password?token={token}&email={email}\n\nThis link expires in 10 minutes.\n\nIf you didn't request this, ignore this email.", ct);

    public async Task SendOtpAsync(string email, string name, string code, CancellationToken ct = default)
        => await SendAsync(email, "SafeDeal — Your OTP code",
            $"Hi {name},\n\nYour OTP code is: {code}\n\nExpires in 10 minutes.", ct);

    public async Task SendTransactionNotificationAsync(string email, string name, string message, CancellationToken ct = default)
        => await SendAsync(email, "SafeDeal — Transaction update", $"Hi {name},\n\n{message}", ct);

    private async Task SendAsync(string to, string subject, string body, CancellationToken ct)
    {
        var host = _config["Email:Host"]!;
        var port = int.Parse(_config["Email:Port"]!);
        var from = _config["Email:From"]!;
        var password = _config["Email:Password"]!;

        using var client = new SmtpClient(host, port)
        {
            Credentials = new NetworkCredential(from, password),
            EnableSsl = true
        };

        var message = new MailMessage(from, to, subject, body);
        await client.SendMailAsync(message, ct);
    }
}