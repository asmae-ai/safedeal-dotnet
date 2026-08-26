using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SafeDeal.Domain.Interfaces.Services;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;

namespace SafeDeal.Tests.Integration;

/// <summary>
/// Monte l'API contre une base et un Redis jetables. Les tests partent donc
/// d'un etat connu et n'abiment pas la base de developpement.
/// Stripe et l'envoi d'e-mails sont remplaces par des doublures : un test ne
/// doit pas dependre d'un service tiers pour verifier une regle metier.
/// </summary>
public class SafeDealFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _db = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("safedeal_test")
        .WithUsername("safedeal")
        .WithPassword("secret")
        .Build();

    private readonly RedisContainer _redis = new RedisBuilder()
        .WithImage("redis:7-alpine")
        .Build();

    public FakePaymentService Payments { get; } = new();
    public FakeEmailService Emails { get; } = new();
    public FakeIdentityVerificationService Identity { get; } = new();

    public async Task InitializeAsync()
    {
        await _db.StartAsync();
        await _redis.StartAsync();
    }

    public new async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _redis.DisposeAsync();
        await base.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.UseSetting("ConnectionStrings:DefaultConnection", _db.GetConnectionString());
        builder.UseSetting("ConnectionStrings:Redis", _redis.GetConnectionString());
        builder.UseSetting("Jwt:Secret", "SafeDeal_Test_Secret_Key_Min_32_Characters!!");
        builder.UseSetting("Jwt:Issuer", "SafeDeal");
        builder.UseSetting("Jwt:Audience", "SafeDeal");
        builder.UseSetting("Frontend:BaseUrl", "http://localhost:5173");
        builder.UseSetting("Platform:CommissionRate", "0.05");

        // Les seuils de production restent testes ailleurs ; ici on veut pouvoir
        // enchainer les scenarios sans que la limitation de debit les masque.
        foreach (var policy in new[]
                 {
                     "login", "register", "otp", "verify-otp", "refresh",
                     "password-reset", "email-verification", "mutations", "webhooks"
                 })
        {
            builder.UseSetting($"RateLimiting:{policy}", "100000");
        }
        builder.UseSetting("Stripe:SecretKey", "sk_test_fake");
        builder.UseSetting("Stripe:WebhookSecret", "whsec_test_fake");

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IPaymentService>();
            services.AddSingleton<IPaymentService>(Payments);
            services.RemoveAll<IEmailService>();
            services.AddSingleton<IEmailService>(Emails);
            services.RemoveAll<IIdentityVerificationService>();
            services.AddSingleton<IIdentityVerificationService>(Identity);
        });
    }

    public HttpClient Anonymous() => CreateClient();

    // Les jetons sont mis en cache par compte : la limitation de debit sur /login
    // (5 tentatives par minute) est une regle de production qu'on ne desactive pas
    // pour les tests, on se contente de ne pas la declencher inutilement.
    private readonly Dictionary<string, string> _tokens = [];
    private readonly SemaphoreSlim _loginLock = new(1, 1);

    public async Task<HttpClient> LoggedInAsync(string email, string password)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await TokenForAsync(email, password));
        return client;
    }

    private async Task<string> TokenForAsync(string email, string password)
    {
        await _loginLock.WaitAsync();
        try
        {
            if (_tokens.TryGetValue(email, out var cached)) return cached;

            var response = await CreateClient().PostAsJsonAsync("/api/v1/login", new { email, password });
            response.EnsureSuccessStatusCode();

            var body = await response.Content.ReadFromJsonAsync<LoginBody>();
            return _tokens[email] = body!.Token;
        }
        finally
        {
            _loginLock.Release();
        }
    }

    private sealed record LoginBody(string Token);
}

internal static class ServiceCollectionTestExtensions
{
    public static void RemoveAll<T>(this IServiceCollection services)
    {
        foreach (var descriptor in services.Where(d => d.ServiceType == typeof(T)).ToList())
            services.Remove(descriptor);
    }
}
