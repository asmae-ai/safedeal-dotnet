using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using SafeDeal.Domain.Interfaces.Services;

namespace SafeDeal.Infrastructure.Services.Identity;

public class SumsubService : IIdentityVerificationService
{
    private readonly IConfiguration _config;
    private readonly HttpClient _httpClient;
    private string AppToken => _config["Sumsub:AppToken"]!;
    private string SecretKey => _config["Sumsub:SecretKey"]!;
    private string BaseUrl => _config["Sumsub:BaseUrl"] ?? "https://api.sumsub.com";

    public SumsubService(IConfiguration config, HttpClient httpClient)
    {
        _config = config;
        _httpClient = httpClient;
        _httpClient.BaseAddress = new Uri(BaseUrl);
    }

    public async Task<string> CreateApplicantAsync(int userId, string email, CancellationToken ct = default)
    {
        var path = "/resources/applicants?levelName=basic-kyc-level";
        var body = new
        {
            externalUserId = userId.ToString(),
            email = email,
            fixedInfo = new { }
        };

        var request = BuildRequest(HttpMethod.Post, path, body);
        var response = await _httpClient.SendAsync(request, ct);
        var content = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
            throw new Exception($"Sumsub CreateApplicant failed: {content}");

        var json = JsonDocument.Parse(content);
        return json.RootElement.GetProperty("id").GetString()!;
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
        var path = $"/resources/applicants/{applicantId}/requiredIdDocsStatus";
        var request = BuildRequest(HttpMethod.Get, path);
        var response = await _httpClient.SendAsync(request, ct);
        var content = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
            return "pending";

        return content;
    }

    private HttpRequestMessage BuildRequest(HttpMethod method, string path, object? body = null)
    {
        var ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var bodyJson = body != null ? JsonSerializer.Serialize(body) : "";
        var signature = Sign(ts, method.Method.ToUpper(), path, bodyJson);

        var request = new HttpRequestMessage(method, path);
        request.Headers.Add("X-App-Token", AppToken);
        request.Headers.Add("X-App-Access-Sig", signature);
        request.Headers.Add("X-App-Access-Ts", ts);

        if (body != null)
            request.Content = new StringContent(bodyJson, Encoding.UTF8, "application/json");

        return request;
    }

    private string Sign(string ts, string method, string path, string body)
    {
        var data = ts + method + path + body;
        var keyBytes = Encoding.UTF8.GetBytes(SecretKey);
        var dataBytes = Encoding.UTF8.GetBytes(data);
        var hash = HMACSHA256.HashData(keyBytes, dataBytes);
        return Convert.ToHexString(hash).ToLower();
    }
}