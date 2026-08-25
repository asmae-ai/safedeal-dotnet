using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using SafeDeal.Application.Transactions.Commands.PayTransaction;

namespace SafeDeal.Tests.Integration;

/// <summary>
/// Parcours complet vendeur → acheteur → clôture, et les règles qui protègent
/// l'argent en chemin. Chaque test nomme le constat d'audit qu'il verrouille.
/// </summary>
[Collection(SafeDealCollection.Name)]
public class EscrowFlowTests
{
    private readonly SafeDealFactory _factory;

    public EscrowFlowTests(SafeDealFactory factory) => _factory = factory;

    private Task<HttpClient> AdminAsync() => _factory.LoggedInAsync("admin@safedeal.com", "Admin@123456");
    private Task<HttpClient> VendorAsync() => _factory.LoggedInAsync("vendor@safedeal.com", "password123");
    private Task<HttpClient> BuyerAsync() => _factory.LoggedInAsync("buyer@safedeal.com", "password123");

    private async Task EnsureVendorVerifiedAsync()
    {
        var vendor = await VendorAsync();
        var me = await vendor.GetFromJsonAsync<MeBody>("/api/v1/me");
        if (me!.User.IdentityStatus == "approved") return;

        var admin = await AdminAsync();
        await admin.PostAsync($"/api/v1/admin/identities/{me.User.Id}/approve", null);
    }

    private async Task<TransactionBody> CreateTransactionAsync(string title = "Casque audio", decimal amount = 1500)
    {
        await EnsureVendorVerifiedAsync();
        var vendor = await VendorAsync();
        var response = await vendor.PostAsJsonAsync("/api/v1/transactions", new { title, amount, currency = "MAD" });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<Envelope<TransactionBody>>())!.Data;
    }

    /// <summary>Simule l'encaissement Stripe sans dépendre d'une signature réelle.</summary>
    private async Task MarkPaidAsync(int transactionId)
    {
        using var scope = _factory.Services.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        await mediator.Send(new PayTransactionCommand(
            transactionId, $"cs_test_{transactionId}", $"pi_test_{transactionId}"));
    }

    private async Task<TransactionBody> PaidTransactionAsync(string title = "Casque audio")
    {
        var tx = await CreateTransactionAsync(title);
        var buyer = await BuyerAsync();
        await buyer.PostAsync($"/api/v1/transactions/{tx.Token}/claim", null);
        await buyer.PostAsync($"/api/v1/transactions/{tx.Id}/checkout", null);
        await MarkPaidAsync(tx.Id);
        return tx;
    }

    private async Task<TransactionBody> StateOfAsync(string token)
        => (await _factory.Anonymous().GetFromJsonAsync<Envelope<TransactionBody>>($"/api/v1/transactions/{token}"))!.Data;

    // ------------------------------------------------------------------ C-01

    [Fact]
    public async Task C01_Le_checkout_renvoie_l_acheteur_sur_la_page_de_sa_transaction()
    {
        var tx = await CreateTransactionAsync();
        var buyer = await BuyerAsync();
        await buyer.PostAsync($"/api/v1/transactions/{tx.Token}/claim", null);

        var response = await buyer.PostAsync($"/api/v1/transactions/{tx.Id}/checkout", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        _factory.Payments.LastSuccessUrl.Should().Be($"http://localhost:5173/pay/{tx.Token}?payment=success");
    }

    // ------------------------------------------------------------------ C-05

    [Fact]
    public async Task C05_Un_tiers_ne_peut_ni_annuler_ni_livrer_ni_cloturer_la_transaction_d_autrui()
    {
        var tx = await CreateTransactionAsync();
        var tiers = await AdminAsync();

        (await tiers.PatchAsync($"/api/v1/transactions/{tx.Id}/cancel", null))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await tiers.PostAsync($"/api/v1/transactions/{tx.Id}/deliver", null))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await tiers.PostAsync($"/api/v1/transactions/{tx.Id}/close", null))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ------------------------------------------------------------------ C-07

    [Fact]
    public async Task C07_Une_transaction_payee_ne_peut_pas_etre_repayee()
    {
        var tx = await PaidTransactionAsync();
        var buyer = await BuyerAsync();

        var response = await buyer.PostAsync($"/api/v1/transactions/{tx.Id}/checkout", null);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task C07_Le_rejeu_du_webhook_est_sans_effet()
    {
        var tx = await PaidTransactionAsync();

        await MarkPaidAsync(tx.Id);

        (await StateOfAsync(tx.Token)).Status.Should().Be("payment_received");
    }

    // ------------------------------------------------------------------ C-09

    [Fact]
    public async Task C09_Une_transition_invalide_sort_en_422_et_non_en_500()
    {
        var tx = await PaidTransactionAsync();
        var buyer = await BuyerAsync();

        var response = await buyer.PostAsync($"/api/v1/transactions/{tx.Id}/close", null);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await response.Content.ReadAsStringAsync()).Should().Contain("Invalid transition");
    }

    // ------------------------------------------------------------------ flux nominal

    [Fact]
    public async Task Le_parcours_complet_libere_les_fonds_et_credite_la_reputation()
    {
        var tx = await PaidTransactionAsync("Montre connectée");
        var vendor = await VendorAsync();
        var buyer = await BuyerAsync();

        (await vendor.PostAsJsonAsync($"/api/v1/transactions/{tx.Id}/ship",
            new { trackingNumber = "CTM-1234", carrier = "CTM" }))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        (await buyer.PostAsync($"/api/v1/transactions/{tx.Id}/deliver", null))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await buyer.PostAsync($"/api/v1/transactions/{tx.Id}/close", null))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        (await StateOfAsync(tx.Token)).Status.Should().Be("closed");

        var me = await (await VendorAsync()).GetFromJsonAsync<MeBody>("/api/v1/me");
        decimal.Parse(me!.User.ReputationScore, CultureInfo.InvariantCulture).Should().BeGreaterThan(0);
    }

    // ------------------------------------------------------------------ contrats

    [Fact]
    public async Task I07_L_expedition_accepte_le_contrat_trackingNumber()
    {
        var tx = await PaidTransactionAsync();
        var vendor = await VendorAsync();

        var response = await vendor.PostAsJsonAsync($"/api/v1/transactions/{tx.Id}/ship",
            new { trackingNumber = "AMANA-99", carrier = "Amana" });

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<Envelope<TransactionBody>>();
        body!.Data.TrackingNumber.Should().Be("AMANA-99");
    }

    [Fact]
    public async Task I06_Les_montants_sortent_toujours_avec_un_point_decimal()
    {
        // Sous une culture serveur francaise, "3200,50" arrivait en NaN cote client.
        var tx = await CreateTransactionAsync("Montre", 3200.5m);

        tx.Amount.Should().Be("3200.50");
    }

    [Fact]
    public async Task Un_vendeur_non_verifie_ne_peut_pas_creer_de_transaction()
    {
        var buyer = await BuyerAsync();

        var response = await buyer.PostAsJsonAsync("/api/v1/transactions",
            new { title = "Tentative", amount = 100m, currency = "MAD" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}

// ---------------------------------------------------------------------- formes de réponse

public record Envelope<T>(T Data);
public record MeBody(UserBody User);
public record UserBody(
    int Id, string Name, string Email, string Role,
    string IdentityStatus, string ReputationScore, string? Phone, bool TwoFactorEnabled);
public record TransactionBody(
    int Id, string Token, string Title, string Amount,
    string Currency, string Status, string? TrackingNumber);
