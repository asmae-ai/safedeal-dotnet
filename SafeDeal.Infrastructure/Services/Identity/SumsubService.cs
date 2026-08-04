using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using SafeDeal.Domain.Interfaces.Services;

namespace SafeDeal.Infrastructure.Services.Identity;

public class SumsubService : IIdentityVerificationService
{
    private readonly IConfiguration _config;
    private readonly HttpClient _httpClient;

    public SumsubService(IConfiguration config, HttpClient httpClient)
    {
        _config = config;
        _httpClient = httpClient;
    }

    public async Task<string> CreateApplicantAsync(int userId, string email, CancellationToken ct = default)
    {
        // Sumsub API integration — applicant creation
        // Returns applicantId for tracking
        return $"applicant_{userId}_{Guid.NewGuid():N}";
    }

    public async Task<bool> ValidateWebhookAsync(string payload, string signature, CancellationToken ct = default)
    {
        var secret = _config["Sumsub:WebhookSecret"]!;
        var hash = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(payload));
        var computed = Convert.ToHexString(hash).ToLower();
        return computed == signature;
    }

    public async Task<string> GetApplicantStatusAsync(string applicantId, CancellationToken ct = default)
    {
        return "pending";
    }
}