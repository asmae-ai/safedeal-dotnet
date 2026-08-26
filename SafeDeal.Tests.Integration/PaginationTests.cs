using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;

namespace SafeDeal.Tests.Integration;

/// <summary>
/// Pagination des listes : premiere page, derniere page, page vide, parametres
/// invalides, gros volume. Et surtout la garantie que les listes deja lues en
/// entier par le frontend le restent tant qu'aucune page n'est demandee.
/// </summary>
[Collection(SafeDealCollection.Name)]
public class PaginationTests
{
    private readonly SafeDealFactory _factory;

    public PaginationTests(SafeDealFactory factory) => _factory = factory;

    private Task<HttpClient> AdminAsync() => _factory.LoggedInAsync("admin@safedeal.com", "Admin@123456");
    private Task<HttpClient> VendorAsync() => _factory.LoggedInAsync("vendor@safedeal.com", "password123");

    private sealed record Meta(int Current_Page, int Last_Page, int Total);
    private sealed record Page<T>(List<T> Data, Meta? Meta);
    private sealed record Item(int Id, string Token, string Title);

    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    private async Task<Page<Item>> GetPageAsync(HttpClient client, string path)
    {
        var response = await client.GetAsync(path);
        response.StatusCode.Should().Be(HttpStatusCode.OK, "une pagination hors bornes se corrige, elle n'echoue pas");
        return JsonSerializer.Deserialize<Page<Item>>(await response.Content.ReadAsStringAsync(), Json)!;
    }

    private async Task EnsureVendorVerifiedAsync()
    {
        var me = await (await VendorAsync()).GetFromJsonAsync<MeBody>("/api/v1/me");
        if (me!.User.IdentityStatus == "approved") return;
        await (await AdminAsync()).PostAsync($"/api/v1/admin/identities/{me.User.Id}/approve", null);
    }

    /// <summary>Garantit au moins <paramref name="count"/> transactions pour le vendeur.</summary>
    private async Task EnsureTransactionsAsync(int count)
    {
        await EnsureVendorVerifiedAsync();
        var vendor = await VendorAsync();

        var existing = (await GetPageAsync(vendor, "/api/v1/transactions?page=1&per_page=1")).Meta!.Total;

        for (var i = existing; i < count; i++)
        {
            var created = await vendor.PostAsJsonAsync("/api/v1/transactions",
                new { title = $"Lot pagination {i}", amount = 100m + i, currency = "MAD" });
            created.EnsureSuccessStatusCode();
        }
    }

    // ------------------------------------------------------------ premiere page

    [Fact]
    public async Task La_premiere_page_rend_le_nombre_demande_et_le_total_reel()
    {
        await EnsureTransactionsAsync(7);
        var vendor = await VendorAsync();

        var page = await GetPageAsync(vendor, "/api/v1/transactions?page=1&per_page=3");

        page.Data.Should().HaveCount(3);
        page.Meta!.Current_Page.Should().Be(1);
        page.Meta.Total.Should().BeGreaterThanOrEqualTo(7);
        page.Meta.Last_Page.Should().Be((int)Math.Ceiling(page.Meta.Total / 3.0));
    }

    [Fact]
    public async Task La_derniere_page_rend_le_reste_sans_deborder()
    {
        await EnsureTransactionsAsync(7);
        var vendor = await VendorAsync();

        var first = await GetPageAsync(vendor, "/api/v1/transactions?page=1&per_page=3");
        var last = await GetPageAsync(vendor, $"/api/v1/transactions?page={first.Meta!.Last_Page}&per_page=3");

        last.Data.Should().NotBeEmpty();
        last.Data.Count.Should().BeLessThanOrEqualTo(3);
        last.Meta!.Current_Page.Should().Be(first.Meta.Last_Page);
    }

    [Fact]
    public async Task Une_page_au_dela_du_total_rend_une_liste_vide_et_non_une_erreur()
    {
        await EnsureTransactionsAsync(3);
        var vendor = await VendorAsync();

        var page = await GetPageAsync(vendor, "/api/v1/transactions?page=9999&per_page=3");

        page.Data.Should().BeEmpty();
        page.Meta!.Total.Should().BeGreaterThan(0, "le total ne depend pas de la page demandee");
    }

    // ---------------------------------------------------------- parametres limites

    [Theory]
    [InlineData("page=0")]
    [InlineData("page=-4")]
    [InlineData("per_page=0")]
    [InlineData("per_page=-10")]
    public async Task Un_parametre_numerique_hors_bornes_est_ramene_dans_les_bornes(string query)
    {
        await EnsureTransactionsAsync(3);
        var vendor = await VendorAsync();

        var response = await vendor.GetAsync($"/api/v1/transactions?{query}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Un_parametre_non_numerique_est_refuse_proprement()
    {
        // Une page « abc » n'est pas une page hors bornes a rattraper : c'est une
        // requete malformee, et elle doit sortir en 400, jamais en 500.
        var vendor = await VendorAsync();

        var response = await vendor.GetAsync("/api/v1/transactions?page=abc");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Une_taille_de_page_demesuree_est_plafonnee()
    {
        // Sans plafond, un client pourrait exiger toute la table en une requete.
        await EnsureTransactionsAsync(3);
        var vendor = await VendorAsync();

        var page = await GetPageAsync(vendor, "/api/v1/transactions?page=1&per_page=100000");

        page.Data.Count.Should().BeLessThanOrEqualTo(100);
    }

    // -------------------------------------------------------------- gros volume

    [Fact]
    public async Task Parcourir_toutes_les_pages_rend_chaque_element_une_seule_fois()
    {
        await EnsureTransactionsAsync(25);
        var vendor = await VendorAsync();

        var first = await GetPageAsync(vendor, "/api/v1/transactions?page=1&per_page=5");
        var seen = new List<int>();

        for (var page = 1; page <= first.Meta!.Last_Page; page++)
        {
            var slice = await GetPageAsync(vendor, $"/api/v1/transactions?page={page}&per_page=5");
            seen.AddRange(slice.Data.Select(t => t.Id));
        }

        seen.Should().OnlyHaveUniqueItems();
        seen.Should().HaveCount(first.Meta.Total);
    }

    // --------------------------------------------------- listes d'administration

    [Fact]
    public async Task La_liste_admin_des_utilisateurs_accepte_une_taille_de_page()
    {
        var admin = await AdminAsync();

        var page = await GetPageAsync(admin, "/api/v1/admin/users?page=1&per_page=2");

        page.Data.Should().HaveCountLessThanOrEqualTo(2);
        page.Meta!.Total.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Les_litiges_restent_une_liste_entiere_tant_qu_aucune_page_n_est_demandee()
    {
        // Contrat historique : le frontend lit « data » sans « meta ».
        var admin = await AdminAsync();

        var body = await admin.GetStringAsync("/api/v1/admin/disputes?status=all");

        body.Should().Contain("\"data\"").And.NotContain("\"meta\"");
    }

    [Fact]
    public async Task Les_litiges_paginent_des_qu_une_page_est_demandee()
    {
        var admin = await AdminAsync();

        var page = await GetPageAsync(admin, "/api/v1/admin/disputes?status=all&page=1&per_page=1");

        page.Meta.Should().NotBeNull();
        page.Data.Should().HaveCountLessThanOrEqualTo(1);
    }

    [Fact]
    public async Task La_file_des_verifications_pagine_a_la_demande()
    {
        var admin = await AdminAsync();

        var whole = await admin.GetStringAsync("/api/v1/admin/identities");
        var paged = await GetPageAsync(admin, "/api/v1/admin/identities?page=1&per_page=1");

        whole.Should().NotContain("\"meta\"");
        paged.Meta.Should().NotBeNull();
    }

    [Fact]
    public async Task Les_notifications_paginent_a_la_demande_sans_changer_le_contrat()
    {
        var vendor = await VendorAsync();

        var whole = await vendor.GetStringAsync("/api/v1/notifications");
        var paged = await GetPageAsync(vendor, "/api/v1/notifications?page=1&per_page=2");

        whole.Should().Contain("\"data\"").And.NotContain("\"meta\"");
        paged.Data.Should().HaveCountLessThanOrEqualTo(2);
    }

    // ----------------------------------------------------------- journal d'audit

    [Fact]
    public async Task Le_journal_d_audit_est_toujours_pagine()
    {
        var admin = await AdminAsync();

        var page = await GetPageAsync(admin, "/api/v1/admin/audit-logs?page=1&per_page=5");

        page.Data.Should().HaveCountLessThanOrEqualTo(5);
        page.Meta!.Total.Should().BeGreaterThan(0, "les connexions des tests ont deja laisse des traces");
    }

    [Fact]
    public async Task Le_journal_d_audit_se_filtre_par_action()
    {
        var admin = await AdminAsync();

        var response = await admin.GetStringAsync("/api/v1/admin/audit-logs?action=Login&per_page=5");

        response.Should().Contain("\"Login\"");
        response.Should().NotContain("\"TransactionShipped\"");
    }

    [Fact]
    public async Task Une_action_inconnue_ne_rend_aucune_trace()
    {
        // Un filtre silencieusement ignore laisserait croire a un journal complet.
        var admin = await AdminAsync();

        var page = await GetPageAsync(admin, "/api/v1/admin/audit-logs?action=CeciNexistePas");

        page.Data.Should().BeEmpty();
        page.Meta!.Total.Should().Be(0);
    }

    [Fact]
    public async Task Le_journal_d_audit_est_reserve_aux_administrateurs()
    {
        var vendor = await VendorAsync();

        (await vendor.GetAsync("/api/v1/admin/audit-logs")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await _factory.Anonymous().GetAsync("/api/v1/admin/audit-logs")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
