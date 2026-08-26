using System.Net;
using System.Text.Json;
using FluentAssertions;

namespace SafeDeal.Tests.Integration;

/// <summary>
/// La documentation est generee depuis le code : ces tests verifient qu'elle
/// decrit bien l'API telle qu'elle est, et qu'elle ne se vide pas en silence
/// le jour ou un commentaire ou un attribut disparait.
/// </summary>
[Collection(SafeDealCollection.Name)]
public class OpenApiDocumentTests
{
    private readonly SafeDealFactory _factory;

    public OpenApiDocumentTests(SafeDealFactory factory) => _factory = factory;

    private async Task<JsonElement> DocumentAsync()
    {
        var response = await _factory.Anonymous().GetAsync("/openapi/v1.json");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.Clone();
    }

    private static JsonElement Operation(JsonElement document, string path, string method)
    {
        document.GetProperty("paths").TryGetProperty(path, out var item)
            .Should().BeTrue($"{path} doit figurer dans la documentation");
        item.TryGetProperty(method, out var operation)
            .Should().BeTrue($"{method.ToUpper()} {path} doit figurer dans la documentation");
        return operation;
    }

    [Fact]
    public async Task Le_document_porte_l_identite_de_l_api()
    {
        var info = (await DocumentAsync()).GetProperty("info");

        info.GetProperty("title").GetString().Should().Be("SafeDeal API");
        info.GetProperty("version").GetString().Should().Be("v1");
        info.GetProperty("description").GetString().Should().Contain("sequestre".Replace("sequestre", "séquestre"));
    }

    [Fact]
    public async Task Chaque_domaine_metier_a_son_groupe()
    {
        var tags = (await DocumentAsync()).GetProperty("tags")
            .EnumerateArray().Select(t => t.GetProperty("name").GetString()).ToList();

        tags.Should().Contain(["Auth", "Transactions", "Disputes", "Identity", "Notifications", "Dashboard", "Admin", "Webhooks"]);
    }

    [Fact]
    public async Task Le_schema_d_authentification_est_declare_une_fois_pour_toutes()
    {
        var scheme = (await DocumentAsync())
            .GetProperty("components").GetProperty("securitySchemes").GetProperty("Bearer");

        scheme.GetProperty("type").GetString().Should().Be("http");
        scheme.GetProperty("scheme").GetString().Should().Be("bearer");
        scheme.GetProperty("bearerFormat").GetString().Should().Be("JWT");
    }

    [Fact]
    public async Task Tous_les_endpoints_publics_sont_documentes()
    {
        var paths = (await DocumentAsync()).GetProperty("paths")
            .EnumerateObject().Select(p => p.Name).ToList();

        paths.Should().Contain([
            "/api/v1/register",
            "/api/v1/login",
            "/api/v1/me",
            "/api/v1/transactions",
            "/api/v1/transactions/{token}/claim",
            "/api/v1/transactions/{id}/dispute",
            "/api/v1/verify-identity",
            "/api/v1/notifications",
            "/api/v1/dashboard/vendor",
            "/api/v1/admin/users",
            "/api/v1/admin/audit-logs",
            "/api/v1/Webhooks/stripe"
        ]);
    }

    [Fact]
    public async Task Chaque_operation_porte_une_description()
    {
        // Une documentation sans texte est une documentation absente.
        var paths = (await DocumentAsync()).GetProperty("paths");

        var undocumented = new List<string>();
        foreach (var path in paths.EnumerateObject())
        {
            foreach (var operation in path.Value.EnumerateObject())
            {
                var hasSummary = operation.Value.TryGetProperty("summary", out var summary)
                                 && !string.IsNullOrWhiteSpace(summary.GetString());
                if (!hasSummary) undocumented.Add($"{operation.Name.ToUpper()} {path.Name}");
            }
        }

        undocumented.Should().BeEmpty();
    }

    [Fact]
    public async Task Une_route_protegee_exige_le_jeton_et_annonce_ses_refus()
    {
        var operation = Operation(await DocumentAsync(), "/api/v1/me", "get");

        operation.GetProperty("security").EnumerateArray()
            .Should().ContainSingle().Which.TryGetProperty("Bearer", out _).Should().BeTrue();

        var responses = operation.GetProperty("responses");
        responses.TryGetProperty("401", out _).Should().BeTrue();
    }

    [Fact]
    public async Task Une_route_reservee_a_un_role_le_dit()
    {
        var operation = Operation(await DocumentAsync(), "/api/v1/dashboard/admin", "get");

        operation.GetProperty("responses").GetProperty("403").GetProperty("description")
            .GetString().Should().Contain("Admin");
        operation.GetProperty("description").GetString().Should().Contain("Admin");
    }

    [Fact]
    public async Task Une_route_publique_n_exige_aucun_jeton()
    {
        // La lecture par jeton securise est la page qu'ouvre un acheteur sans compte.
        var operation = Operation(await DocumentAsync(), "/api/v1/transactions/{token}", "get");

        operation.TryGetProperty("security", out _).Should().BeFalse();
    }

    [Fact]
    public async Task Une_route_limitee_en_debit_annonce_son_429()
    {
        var operation = Operation(await DocumentAsync(), "/api/v1/login", "post");

        operation.GetProperty("responses").TryGetProperty("429", out var tooMany).Should().BeTrue();
        tooMany.GetProperty("description").GetString().Should().Contain("login");
    }

    [Fact]
    public async Task Une_ecriture_annonce_son_422()
    {
        // Validation et regles metier sortent toutes deux en 422 : le client doit
        // savoir qu'il aura ce cas a traiter.
        var operation = Operation(await DocumentAsync(), "/api/v1/register", "post");

        operation.GetProperty("responses").TryGetProperty("422", out _).Should().BeTrue();
    }

    [Fact]
    public async Task Les_parametres_de_pagination_sont_documentes()
    {
        var operation = Operation(await DocumentAsync(), "/api/v1/admin/users", "get");

        var names = operation.GetProperty("parameters")
            .EnumerateArray().Select(p => p.GetProperty("name").GetString()).ToList();

        names.Should().Contain(["page", "per_page", "search", "role"]);
    }

    [Fact]
    public async Task La_sonde_d_etat_ne_figure_pas_dans_la_documentation_metier()
    {
        // /health s'adresse a l'exploitation, pas aux clients de l'API.
        var paths = (await DocumentAsync()).GetProperty("paths")
            .EnumerateObject().Select(p => p.Name).ToList();

        paths.Should().NotContain("/health");
    }
}
