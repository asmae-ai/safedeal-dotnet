namespace SafeDeal.Domain.Interfaces.Services;

public interface IOtpService
{
    Task<string> GenerateAndStoreAsync(string key, CancellationToken ct = default);
    Task<bool> ValidateAsync(string key, string code, CancellationToken ct = default);
    Task InvalidateAsync(string key, CancellationToken ct = default);
    Task<bool> IsOnCooldownAsync(string key, CancellationToken ct = default);
}