using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using SafeDeal.Application.Transactions.Commands.PayTransaction;

namespace SafeDeal.Tests.Integration;

/// <summary>
/// Qui a le droit de faire quoi. Le sequestre repose entierement sur ces
/// frontieres : un acheteur qui pourrait cloturer, ou un tiers qui pourrait
/// lire un litige, videraient la garantie de son sens.
/// </summary>
[Collection(SafeDealCollection.Name)]
public class PermissionTests
{
    private readonly SafeDealFactory _factory;

    public PermissionTests(SafeDealFactory factory) => _factory = factory;

    private Task<HttpClient> AdminAsync() => _factory.LoggedInAsync("admin@safedeal.com", "Admin@123456");
    private Task<HttpClient> VendorAsync() => _factory.LoggedInAsync("vendor@safedeal.com", "password123");
    private Task<HttpClient> BuyerAsync() => _factory.LoggedInAsync("buyer@safedeal.com", "password123");

    private async Task<TransactionBody> PaidTransactionAsync(string title)
    {
        var vendor = await VendorAsync();
        var me = await vendor.GetFromJsonAsync<MeBody>("/api/v1/me");
        if (me!.User.IdentityStatus != "approved")
            await (await AdminAsync()).PostAsync($"/api/v1/admin/identities/{me.User.Id}/approve", null);

        var created = await vendor.PostAsJsonAsync("/api/v1/transactions",
            new { title, amount = 900m, currency = "MAD" });
        created.EnsureSuccessStatusCode();
        var tx = (await created.Content.ReadFromJsonAsync<Envelope<TransactionBody>>())!.Data;

        var buyer = await BuyerAsync();
        await buyer.PostAsync($"/api/v1/transactions/{tx.Token}/claim", null);
        await buyer.PostAsync($"/api/v1/transactions/{tx.Id}/checkout", null);

        using var scope = _factory.Services.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        await mediator.Send(new PayTransactionCommand(tx.Id, $"cs_{tx.Id}", $"pi_{tx.Id}"));

        return tx;
    }

    // --------------------------------------------------------------- sans jeton

    [Theory]
    [InlineData("/api/v1/me")]
    [InlineData("/api/v1/transactions")]
    [InlineData("/api/v1/notifications")]
    [InlineData("/api/v1/dashboard/buyer")]
    [InlineData("/api/v1/admin/stats")]
    [InlineData("/api/v1/verify-identity/status")]
    public async Task Un_visiteur_anonyme_est_refuse_sur_les_ecrans_proteges(string path)
    {
        var response = await _factory.Anonymous().GetAsync(path);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Un_jeton_falsifie_ne_donne_acces_a_rien()
    {
        var client = _factory.Anonymous();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", "eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiIxIn0.signature-inventee");

        var response = await client.GetAsync("/api/v1/me");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ------------------------------------------------------------------- roles

    [Fact]
    public async Task Chaque_tableau_de_bord_est_reserve_a_son_role()
    {
        var buyer = await BuyerAsync();
        var vendor = await VendorAsync();
        var admin = await AdminAsync();

        (await buyer.GetAsync("/api/v1/dashboard/vendor")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await vendor.GetAsync("/api/v1/dashboard/buyer")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await buyer.GetAsync("/api/v1/dashboard/admin")).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        (await buyer.GetAsync("/api/v1/dashboard/buyer")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await vendor.GetAsync("/api/v1/dashboard/vendor")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await admin.GetAsync("/api/v1/dashboard/admin")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Les_decisions_d_identite_sont_reservees_aux_admins()
    {
        var buyer = await BuyerAsync();

        (await buyer.PostAsync("/api/v1/admin/identities/1/approve", null))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await buyer.PostAsJsonAsync("/api/v1/admin/identities/1/reject", new { reason = "Non." }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await buyer.GetAsync("/api/v1/admin/identities/1/document/front"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Trancher_un_litige_est_reserve_aux_admins()
    {
        var tx = await PaidTransactionAsync("Enceinte");
        var buyer = await BuyerAsync();
        await buyer.PostAsync($"/api/v1/transactions/{tx.Id}/dispute", new MultipartFormDataContent
        {
            { new StringContent("damaged"), "category" },
            { new StringContent("Article casse."), "description" },
        });

        var response = await buyer.PostAsJsonAsync($"/api/v1/admin/disputes/1/resolve",
            new { decision = "refunded", note = "Je me rembourse." });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // -------------------------------------------------------------- possession

    [Fact]
    public async Task Seul_le_vendeur_expedie_sa_transaction()
    {
        var tx = await PaidTransactionAsync("Ordinateur portable");
        var buyer = await BuyerAsync();

        var response = await buyer.PostAsJsonAsync($"/api/v1/transactions/{tx.Id}/ship",
            new { trackingNumber = "TRK-1", carrier = "CTM" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Seul_l_acheteur_constate_la_livraison_et_cloture()
    {
        // Cloturer libere les fonds vers le vendeur : le vendeur ne peut pas se
        // payer lui-meme en declarant la livraison recue.
        var tx = await PaidTransactionAsync("Tablette");
        var vendor = await VendorAsync();
        await vendor.PostAsJsonAsync($"/api/v1/transactions/{tx.Id}/ship",
            new { trackingNumber = "TRK-2", carrier = "CTM" });

        (await vendor.PostAsync($"/api/v1/transactions/{tx.Id}/deliver", null))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await vendor.PostAsync($"/api/v1/transactions/{tx.Id}/close", null))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Un_tiers_ne_peut_pas_lire_le_litige_d_autrui()
    {
        var tx = await PaidTransactionAsync("Velo");
        var buyer = await BuyerAsync();
        await buyer.PostAsync($"/api/v1/transactions/{tx.Id}/dispute", new MultipartFormDataContent
        {
            { new StringContent("damaged"), "category" },
            { new StringContent("Cadre tordu."), "description" },
        });

        var tiers = await AdminAsync();
        var response = await tiers.GetAsync($"/api/v1/transactions/{tx.Id}/dispute");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Un_tiers_ne_peut_pas_verser_de_piece_au_litige_d_autrui()
    {
        var tx = await PaidTransactionAsync("Guitare electrique");
        var buyer = await BuyerAsync();
        await buyer.PostAsync($"/api/v1/transactions/{tx.Id}/dispute", new MultipartFormDataContent
        {
            { new StringContent("damaged"), "category" },
            { new StringContent("Manche fendu."), "description" },
        });

        var tiers = await AdminAsync();
        var response = await tiers.PostAsync($"/api/v1/transactions/{tx.Id}/dispute/evidence",
            new MultipartFormDataContent { { new StringContent("Mon avis sur ce dossier."), "description" } });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task La_liste_des_transactions_ne_montre_que_les_siennes()
    {
        var tx = await PaidTransactionAsync("Console de jeu");
        var admin = await AdminAsync();

        var list = await admin.GetFromJsonAsync<ListEnvelope<TransactionBody>>("/api/v1/transactions");

        list!.Data.Should().NotContain(t => t.Id == tx.Id,
            "l'administrateur n'est partie ni comme vendeur ni comme acheteur");
    }

    [Fact]
    public async Task Une_notification_appartenant_a_autrui_reste_introuvable()
    {
        // 404 et non 403 : repondre « interdit » confirmerait l'existence de la
        // notification et donc l'activite d'un autre compte.
        await PaidTransactionAsync("Imprimante");
        var vendor = await VendorAsync();
        var notifications = await vendor.GetFromJsonAsync<ListEnvelope<NotificationBody>>("/api/v1/notifications");
        var target = notifications!.Data.First();

        var buyer = await BuyerAsync();
        var response = await buyer.PostAsync($"/api/v1/notifications/{target.Id}/read", null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ------------------------------------------------------------- revocation

    [Fact]
    public async Task Un_jeton_revoque_par_deconnexion_ne_sert_plus()
    {
        // Le jeton reste cryptographiquement valide jusqu'a son echeance :
        // seule la liste noire le neutralise immediatement.
        var email = $"revoke{Guid.NewGuid():N}@safedeal.test";
        var anonymous = _factory.Anonymous();
        var registered = await anonymous.PostAsJsonAsync("/api/v1/register", new
        {
            name = "Session courte",
            email,
            password = "password123",
            passwordConfirmation = "password123",
            role = "buyer",
        });
        registered.EnsureSuccessStatusCode();
        var token = (await registered.Content.ReadFromJsonAsync<TokenBody>())!.Token;

        var client = _factory.Anonymous();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        (await client.GetAsync("/api/v1/me")).StatusCode.Should().Be(HttpStatusCode.OK);

        await client.PostAsync("/api/v1/logout", null);

        (await client.GetAsync("/api/v1/me")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private sealed record TokenBody(string Token);
    private sealed record NotificationBody(int Id, bool IsRead);
    private sealed record ListEnvelope<T>(List<T> Data);
}
