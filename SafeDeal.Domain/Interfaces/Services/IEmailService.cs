namespace SafeDeal.Domain.Interfaces.Services;

public interface IEmailService
{
    Task SendVerificationCodeAsync(string email, string name, string code, CancellationToken ct = default);
    Task SendPasswordResetAsync(string email, string name, string token, CancellationToken ct = default);
    Task SendOtpAsync(string email, string name, string code, CancellationToken ct = default);
    Task SendTransactionNotificationAsync(string email, string name, string message, CancellationToken ct = default);
}