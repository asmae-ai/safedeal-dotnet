namespace SafeDeal.Domain.Interfaces.Services;

public interface IIdentityVerificationService
{
    Task<string> CreateApplicantAsync(int userId, string email, CancellationToken ct = default);
    Task<bool> ValidateWebhookAsync(string payload, string signature, CancellationToken ct = default);
    Task<string> GetApplicantStatusAsync(string applicantId, CancellationToken ct = default);
}