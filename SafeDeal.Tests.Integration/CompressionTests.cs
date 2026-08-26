using System.IO.Compression;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace SafeDeal.Tests.Integration;

/// <summary>
/// La compression doit alleger les reponses sans jamais en changer le contenu :
/// un client qui ne la demande pas doit recevoir exactement ce qu'il recevait
/// avant, et un client qui la demande doit retrouver le meme JSON une fois
/// decompresse.
/// </summary>
[Collection(SafeDealCollection.Name)]
public class CompressionTests
{
    private readonly SafeDealFactory _factory;

    public CompressionTests(SafeDealFactory factory) => _factory = factory;

    private Task<HttpClient> AdminAsync() => _factory.LoggedInAsync("admin@safedeal.com", "Admin@123456");
    private Task<HttpClient> VendorAsync() => _factory.LoggedInAsync("vendor@safedeal.com", "password123");

    private static void Accept(HttpClient client, params string[] encodings)
    {
        client.DefaultRequestHeaders.AcceptEncoding.Clear();
        foreach (var encoding in encodings)
            client.DefaultRequestHeaders.AcceptEncoding.Add(new StringWithQualityHeaderValue(encoding));
    }

    private static async Task<string> DecompressAsync(HttpResponseMessage response)
    {
        var encoding = response.Content.Headers.ContentEncoding.FirstOrDefault();
        await using var raw = await response.Content.ReadAsStreamAsync();

        Stream stream = encoding switch
        {
            "gzip" => new GZipStream(raw, CompressionMode.Decompress),
            "br" => new BrotliStream(raw, CompressionMode.Decompress),
            _ => raw
        };

        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync();
    }

    // ------------------------------------------------------------------ negociation

    [Fact]
    public async Task Un_client_qui_demande_gzip_recoit_une_reponse_compressee()
    {
        var admin = await AdminAsync();
        Accept(admin, "gzip");

        var response = await admin.GetAsync("/api/v1/admin/stats");

        response.Content.Headers.ContentEncoding.Should().Contain("gzip");
    }

    [Fact]
    public async Task Brotli_est_prefere_quand_le_client_l_accepte()
    {
        // Brotli compresse mieux a cout comparable : il passe en premier.
        var admin = await AdminAsync();
        Accept(admin, "br", "gzip");

        var response = await admin.GetAsync("/api/v1/admin/stats");

        response.Content.Headers.ContentEncoding.Should().Contain("br");
    }

    [Fact]
    public async Task Un_client_qui_ne_demande_rien_recoit_une_reponse_intacte()
    {
        // C'est le cas du frontend existant s'il n'annonce aucun encodage :
        // rien ne doit changer pour lui.
        var admin = await AdminAsync();
        Accept(admin);

        var response = await admin.GetAsync("/api/v1/admin/stats");

        response.Content.Headers.ContentEncoding.Should().BeEmpty();
        (await response.Content.ReadAsStringAsync()).Should().StartWith("{");
    }

    // -------------------------------------------------------------- integrite

    [Fact]
    public async Task Le_json_decompresse_est_identique_au_json_non_compresse()
    {
        var plain = await AdminAsync();
        Accept(plain);
        var reference = await (await plain.GetAsync("/api/v1/admin/stats")).Content.ReadAsStringAsync();

        var compressed = await AdminAsync();
        Accept(compressed, "gzip");
        var response = await compressed.GetAsync("/api/v1/admin/stats");

        (await DecompressAsync(response)).Should().Be(reference);
    }

    [Fact]
    public async Task Une_reponse_compressee_reste_du_json_exploitable()
    {
        var vendor = await VendorAsync();
        Accept(vendor, "br");

        var response = await vendor.GetAsync("/api/v1/transactions");

        var body = JsonDocument.Parse(await DecompressAsync(response));
        body.RootElement.TryGetProperty("data", out _).Should().BeTrue();
        body.RootElement.TryGetProperty("meta", out _).Should().BeTrue();
    }

    [Fact]
    public async Task Une_erreur_reste_lisible_une_fois_compressee()
    {
        // Le middleware d'erreurs ecrit sa reponse derriere la compression :
        // un 404 compresse doit rester un message exploitable.
        var client = _factory.Anonymous();
        Accept(client, "gzip");

        var response = await client.GetAsync("/api/v1/transactions/jeton-inexistant");

        var body = JsonDocument.Parse(await DecompressAsync(response));
        body.RootElement.TryGetProperty("message", out _).Should().BeTrue();
    }

    [Fact]
    public async Task La_documentation_reste_servie_correctement()
    {
        var client = _factory.Anonymous();
        Accept(client, "gzip");

        var response = await client.GetAsync("/openapi/v1.json");

        var document = JsonDocument.Parse(await DecompressAsync(response));
        document.RootElement.GetProperty("info").GetProperty("title").GetString().Should().Be("SafeDeal API");
    }

    [Fact]
    public async Task La_sonde_d_etat_reste_lisible()
    {
        var client = _factory.Anonymous();
        Accept(client, "gzip");

        var response = await client.GetAsync("/health");

        var body = JsonDocument.Parse(await DecompressAsync(response));
        body.RootElement.GetProperty("status").GetString().Should().Be("Healthy");
    }

    // -------------------------------------------------------------- rendement

    [Fact]
    public async Task Une_grosse_reponse_json_est_reellement_allegee()
    {
        var admin = await AdminAsync();

        Accept(admin);
        var plain = await (await admin.GetAsync("/api/v1/dashboard/admin")).Content.ReadAsByteArrayAsync();

        Accept(admin, "gzip");
        var compressed = await (await admin.GetAsync("/api/v1/dashboard/admin")).Content.ReadAsByteArrayAsync();

        compressed.Length.Should().BeLessThan(plain.Length,
            "un tableau de bord est du JSON tres repetitif");
    }

    [Fact]
    public void Les_binaires_deja_compresses_sont_exclus()
    {
        // Recompresser un PNG ou un PDF coute du temps processeur pour, au mieux,
        // quelques octets : avatars, pieces d'identite et preuves de litige
        // partent tels quels.
        var options = _factory.Services
            .GetRequiredService<IOptions<ResponseCompressionOptions>>().Value;

        options.ExcludedMimeTypes.Should().Contain(["image/png", "image/jpeg", "application/pdf"]);
        options.MimeTypes.Should().Contain("application/json");
    }

    [Fact]
    public void La_compression_reste_desactivee_sur_une_connexion_chiffree_par_defaut()
    {
        // Compresser une reponse chiffree qui melange un jeton et une valeur
        // controlee par l'appelant ouvre la voie a BREACH. En production, TLS est
        // termine par le proxy et la compression s'applique cote HTTP.
        var options = _factory.Services
            .GetRequiredService<IOptions<ResponseCompressionOptions>>().Value;

        options.EnableForHttps.Should().BeFalse();
    }
}
