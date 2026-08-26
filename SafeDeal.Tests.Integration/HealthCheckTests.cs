using System.Net;
using System.Text.Json;
using FluentAssertions;

namespace SafeDeal.Tests.Integration;

/// <summary>
/// La sonde d'etat est publique : elle doit renseigner l'exploitant sans rien
/// livrer a un visiteur anonyme.
/// </summary>
[Collection(SafeDealCollection.Name)]
public class HealthCheckTests
{
    private readonly SafeDealFactory _factory;

    public HealthCheckTests(SafeDealFactory factory) => _factory = factory;

    private sealed record HealthBody(string Status, double TotalDurationMs, HealthEntry[] Checks);
    private sealed record HealthEntry(string Name, string Status, double DurationMs, string? Description);

    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    private async Task<(HttpResponseMessage Response, HealthBody Body, string Raw)> GetAsync(string path)
    {
        var response = await _factory.Anonymous().GetAsync(path);
        var raw = await response.Content.ReadAsStringAsync();
        return (response, JsonSerializer.Deserialize<HealthBody>(raw, Json)!, raw);
    }

    [Fact]
    public async Task Health_repond_sans_authentification()
    {
        var (response, body, _) = await GetAsync("/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body.Status.Should().Be("Healthy");
    }

    [Fact]
    public async Task Health_couvre_l_api_la_base_et_redis()
    {
        var (_, body, _) = await GetAsync("/health");

        body.Checks.Select(c => c.Name).Should().BeEquivalentTo(["api", "postgres", "redis"]);
        body.Checks.Should().OnlyContain(c => c.Status == "Healthy");
    }

    [Fact]
    public async Task Health_live_ignore_les_dependances_externes()
    {
        // Une base indisponible ne doit pas faire redemarrer un processus sain :
        // la sonde de vie ne regarde que l'API.
        var (response, body, _) = await GetAsync("/health/live");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body.Checks.Should().ContainSingle().Which.Name.Should().Be("api");
    }

    [Fact]
    public async Task Health_n_expose_ni_secret_ni_detail_d_infrastructure()
    {
        var (_, _, raw) = await GetAsync("/health");

        foreach (var leak in new[] { "Password", "password", "Username", "Host=", "sk_test", "Jwt", "Secret", "Exception", "StackTrace" })
        {
            raw.Should().NotContain(leak);
        }
    }

    [Fact]
    public async Task Health_annonce_des_durees_mesurees()
    {
        var (_, body, _) = await GetAsync("/health");

        body.TotalDurationMs.Should().BeGreaterThanOrEqualTo(0);
        body.Checks.Should().OnlyContain(c => c.DurationMs >= 0);
    }
}
