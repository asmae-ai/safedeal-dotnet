using System.Net.Http.Json;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using SafeDeal.Application.Transactions.Commands.PayTransaction;
using StackExchange.Redis;

namespace SafeDeal.Tests.Integration;

/// <summary>
/// Le cache doit accelerer les tableaux de bord sans jamais montrer un etat
/// perime apres une ecriture, et sans jamais s'interposer sur une donnee
/// transactionnelle.
/// </summary>
[Collection(SafeDealCollection.Name)]
public class CacheTests
{
    private readonly SafeDealFactory _factory;

    public CacheTests(SafeDealFactory factory) => _factory = factory;

    private Task<HttpClient> AdminAsync() => _factory.LoggedInAsync("admin@safedeal.com", "Admin@123456");
    private Task<HttpClient> VendorAsync() => _factory.LoggedInAsync("vendor@safedeal.com", "password123");
    private Task<HttpClient> BuyerAsync() => _factory.LoggedInAsync("buyer@safedeal.com", "password123");

    private sealed record VendorDashboard(int TotalOrders, string InEscrow);
    private sealed record BuyerDashboard(int TotalOrders, string InEscrow);

    private IConnectionMultiplexer Redis => _factory.Services.GetRequiredService<IConnectionMultiplexer>();

    private string[] CacheKeys(string pattern)
    {
        var endpoint = Redis.GetEndPoints().First();
        return Redis.GetServer(endpoint).Keys(pattern: pattern).Select(k => k.ToString()).ToArray();
    }

    private async Task<int> VendorIdAsync()
    {
        var me = await (await VendorAsync()).GetFromJsonAsync<MeBody>("/api/v1/me");
        return me!.User.Id;
    }

    private async Task EnsureVendorVerifiedAsync()
    {
        var me = await (await VendorAsync()).GetFromJsonAsync<MeBody>("/api/v1/me");
        if (me!.User.IdentityStatus == "approved") return;
        await (await AdminAsync()).PostAsync($"/api/v1/admin/identities/{me.User.Id}/approve", null);
    }

    private async Task<TransactionBody> CreateTransactionAsync(string title)
    {
        await EnsureVendorVerifiedAsync();
        var response = await (await VendorAsync()).PostAsJsonAsync("/api/v1/transactions",
            new { title, amount = 700m, currency = "MAD" });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<Envelope<TransactionBody>>())!.Data;
    }

    private async Task MarkPaidAsync(int transactionId)
    {
        using var scope = _factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IMediator>()
            .Send(new PayTransactionCommand(transactionId, $"cs_{transactionId}", $"pi_{transactionId}"));
    }

    // ----------------------------------------------------------------- lecture

    [Fact]
    public async Task Le_tableau_de_bord_vendeur_atterrit_dans_redis()
    {
        var vendorId = await VendorIdAsync();
        await EnsureVendorVerifiedAsync();

        await (await VendorAsync()).GetAsync("/api/v1/dashboard/vendor");

        CacheKeys($"cache:dash:u:{vendorId}:*:vendor").Should().NotBeEmpty();
    }

    [Fact]
    public async Task Les_compteurs_d_administration_atterrissent_dans_redis()
    {
        await (await AdminAsync()).GetAsync("/api/v1/admin/stats");

        CacheKeys("cache:dash:admin:*:stats").Should().NotBeEmpty();
    }

    [Fact]
    public async Task Deux_lectures_successives_rendent_la_meme_reponse()
    {
        await EnsureVendorVerifiedAsync();
        var vendor = await VendorAsync();

        var first = await vendor.GetFromJsonAsync<Envelope<VendorDashboard>>("/api/v1/dashboard/vendor");
        var second = await vendor.GetFromJsonAsync<Envelope<VendorDashboard>>("/api/v1/dashboard/vendor");

        second.Should().BeEquivalentTo(first);
    }

    // ------------------------------------------------------------ invalidation

    [Fact]
    public async Task Une_nouvelle_transaction_se_voit_immediatement()
    {
        // Sans invalidation, le vendeur attendrait l'expiration pour voir sa
        // propre transaction : c'est exactement le defaut que le cache ne doit
        // pas introduire.
        await EnsureVendorVerifiedAsync();
        var vendor = await VendorAsync();
        var before = await vendor.GetFromJsonAsync<Envelope<VendorDashboard>>("/api/v1/dashboard/vendor");

        await CreateTransactionAsync("Imprimante laser");

        var after = await vendor.GetFromJsonAsync<Envelope<VendorDashboard>>("/api/v1/dashboard/vendor");
        after!.Data.TotalOrders.Should().Be(before!.Data.TotalOrders + 1);
    }

    [Fact]
    public async Task Un_encaissement_se_voit_immediatement_chez_les_deux_parties()
    {
        var tx = await CreateTransactionAsync("Ecran 27 pouces");
        var buyer = await BuyerAsync();
        await buyer.PostAsync($"/api/v1/transactions/{tx.Token}/claim", null);

        var vendor = await VendorAsync();
        var vendorBefore = await vendor.GetFromJsonAsync<Envelope<VendorDashboard>>("/api/v1/dashboard/vendor");
        var buyerBefore = await buyer.GetFromJsonAsync<Envelope<BuyerDashboard>>("/api/v1/dashboard/buyer");

        await MarkPaidAsync(tx.Id);

        var vendorAfter = await vendor.GetFromJsonAsync<Envelope<VendorDashboard>>("/api/v1/dashboard/vendor");
        var buyerAfter = await buyer.GetFromJsonAsync<Envelope<BuyerDashboard>>("/api/v1/dashboard/buyer");

        static decimal Escrow(string amount) =>
            decimal.Parse(amount, System.Globalization.CultureInfo.InvariantCulture);

        Escrow(vendorAfter!.Data.InEscrow).Should().BeGreaterThan(Escrow(vendorBefore!.Data.InEscrow));
        Escrow(buyerAfter!.Data.InEscrow).Should().BeGreaterThan(Escrow(buyerBefore!.Data.InEscrow));
    }

    [Fact]
    public async Task Une_reclamation_par_un_acheteur_perime_la_vue_du_vendeur()
    {
        var tx = await CreateTransactionAsync("Machine a cafe");
        var vendor = await VendorAsync();
        await vendor.GetAsync("/api/v1/dashboard/vendor");
        var vendorId = await VendorIdAsync();
        var keysBefore = CacheKeys($"cache:dash:u:{vendorId}:*:vendor");

        await (await BuyerAsync()).PostAsync($"/api/v1/transactions/{tx.Token}/claim", null);
        await vendor.GetAsync("/api/v1/dashboard/vendor");

        // La generation a change : la nouvelle lecture ne s'ecrit plus sous la
        // meme cle que l'ancienne.
        CacheKeys($"cache:dash:u:{vendorId}:*:vendor").Should().NotBeEquivalentTo(keysBefore);
    }

    [Fact]
    public async Task Une_decision_d_identite_perime_les_files_d_administration()
    {
        var admin = await AdminAsync();
        await admin.GetAsync("/api/v1/admin/identities/stats");
        var before = CacheKeys("cache:dash:admin:*:stats:identity");

        var vendorId = await VendorIdAsync();
        await admin.PostAsync($"/api/v1/admin/identities/{vendorId}/approve", null);
        await admin.GetAsync("/api/v1/admin/identities/stats");

        CacheKeys("cache:dash:admin:*:stats:identity").Should().NotBeEquivalentTo(before);
    }

    // ------------------------------------------------------- frontiere du cache

    [Fact]
    public async Task L_etat_d_une_transaction_ne_passe_jamais_par_le_cache()
    {
        // Le sequestre se lit toujours dans la base : une transaction payee ne
        // doit pas pouvoir apparaitre « en attente de paiement » pendant une
        // fenetre de peremption.
        var tx = await CreateTransactionAsync("Casque a reduction de bruit");
        var buyer = await BuyerAsync();
        await buyer.PostAsync($"/api/v1/transactions/{tx.Token}/claim", null);

        var before = await _factory.Anonymous()
            .GetFromJsonAsync<Envelope<TransactionBody>>($"/api/v1/transactions/{tx.Token}");
        before!.Data.Status.Should().Be("pending_payment");

        await MarkPaidAsync(tx.Id);

        var after = await _factory.Anonymous()
            .GetFromJsonAsync<Envelope<TransactionBody>>($"/api/v1/transactions/{tx.Token}");
        after!.Data.Status.Should().Be("payment_received");

        CacheKeys("cache:*").Should().NotContain(k => k.Contains("transaction", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task La_liste_des_transactions_n_est_pas_mise_en_cache()
    {
        var vendor = await VendorAsync();
        await vendor.GetAsync("/api/v1/transactions");
        var tx = await CreateTransactionAsync("Souris ergonomique");

        var list = await vendor.GetStringAsync("/api/v1/transactions?page=1");

        list.Should().Contain(tx.Token, "une liste paginee se lit dans la base, pas dans le cache");
    }
}
