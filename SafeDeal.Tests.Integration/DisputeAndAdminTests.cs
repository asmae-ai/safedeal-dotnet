using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using SafeDeal.Application.Transactions.Commands.PayTransaction;

namespace SafeDeal.Tests.Integration;

/// <summary>
/// Règles du litige et des écrans d'administration : c'est là que les fonds
/// sont gelés puis attribués, donc là que les garanties comptent le plus.
/// </summary>
[Collection(SafeDealCollection.Name)]
public class DisputeAndAdminTests
{
    private readonly SafeDealFactory _factory;

    public DisputeAndAdminTests(SafeDealFactory factory) => _factory = factory;

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
            new { title, amount = 2000m, currency = "MAD" });
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

    private static MultipartFormDataContent DisputeForm(string category, string description)
    {
        var form = new MultipartFormDataContent
        {
            { new StringContent(category), "category" },
            { new StringContent(description), "description" },
        };
        return form;
    }

    private async Task<HttpResponseMessage> OpenDisputeAsync(HttpClient client, int transactionId, string description)
        => await client.PostAsync($"/api/v1/transactions/{transactionId}/dispute",
            DisputeForm("damaged", description));

    // ------------------------------------------------------------------ C-02 / C-04

    [Fact]
    public async Task C04_Ouvrir_un_litige_gele_la_transaction()
    {
        var tx = await PaidTransactionAsync("Litige gel");
        var buyer = await BuyerAsync();

        var response = await OpenDisputeAsync(buyer, tx.Id, "Le produit est arrivé cassé.");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var state = await _factory.Anonymous()
            .GetFromJsonAsync<Envelope<TransactionBody>>($"/api/v1/transactions/{tx.Token}");
        state!.Data.Status.Should().Be("dispute");
    }

    // ------------------------------------------------------------------ C-06

    [Fact]
    public async Task C06_Une_transaction_ne_porte_qu_un_seul_litige()
    {
        var tx = await PaidTransactionAsync("Litige unique");
        var buyer = await BuyerAsync();
        await OpenDisputeAsync(buyer, tx.Id, "Première réclamation.");

        var second = await OpenDisputeAsync(buyer, tx.Id, "Seconde tentative.");

        second.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Un_tiers_ne_peut_pas_ouvrir_de_litige()
    {
        var tx = await PaidTransactionAsync("Litige tiers");
        var tiers = await AdminAsync();

        var response = await OpenDisputeAsync(tiers, tx.Id, "Réclamation d'un tiers.");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ------------------------------------------------------------------ C-03 / I-12

    [Fact]
    public async Task C03_La_reponse_du_vendeur_est_persistee_avec_son_auteur()
    {
        var tx = await PaidTransactionAsync("Litige echange");
        var buyer = await BuyerAsync();
        var vendor = await VendorAsync();
        await OpenDisputeAsync(buyer, tx.Id, "Le colis est arrivé endommagé.");

        var evidence = new MultipartFormDataContent
        {
            { new StringContent("Le colis a été remis intact au transporteur."), "description" },
        };
        var response = await vendor.PostAsync($"/api/v1/transactions/{tx.Id}/dispute/evidence", evidence);
        response.EnsureSuccessStatusCode();

        var dispute = await buyer.GetFromJsonAsync<Envelope<DisputeBody>>($"/api/v1/transactions/{tx.Id}/dispute");
        dispute!.Data.Evidences.Should().HaveCount(2);
        dispute.Data.Evidences[0].SubmittedBy.Should().Be("buyer");
        dispute.Data.Evidences[1].SubmittedBy.Should().Be("vendor");
        dispute.Data.Evidences[1].Description.Should().Contain("remis intact");
        dispute.Data.Status.Should().Be("underreview");
    }

    // ------------------------------------------------------------------ C-08

    [Fact]
    public async Task C08_Trancher_en_faveur_de_l_acheteur_declenche_le_remboursement()
    {
        var tx = await PaidTransactionAsync("Litige rembourse");
        var buyer = await BuyerAsync();
        var admin = await AdminAsync();
        await OpenDisputeAsync(buyer, tx.Id, "Produit non conforme.");

        var disputes = await admin.GetFromJsonAsync<Envelope<List<AdminDisputeBody>>>("/api/v1/admin/disputes");
        var dispute = disputes!.Data.First(d => d.TransactionId == tx.Id);

        var response = await admin.PostAsJsonAsync($"/api/v1/admin/disputes/{dispute.Id}/resolve",
            new { decision = "refunded", note = "Preuves de l'acheteur retenues." });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        _factory.Payments.Refunds.Should().Contain($"pi_{tx.Id}");

        var state = await _factory.Anonymous()
            .GetFromJsonAsync<Envelope<TransactionBody>>($"/api/v1/transactions/{tx.Token}");
        state!.Data.Status.Should().Be("refunded");
    }

    [Fact]
    public async Task C08_Un_remboursement_refuse_par_le_prestataire_laisse_le_litige_ouvert()
    {
        var tx = await PaidTransactionAsync("Litige refus stripe");
        var buyer = await BuyerAsync();
        var admin = await AdminAsync();
        await OpenDisputeAsync(buyer, tx.Id, "Produit jamais reçu.");

        var disputes = await admin.GetFromJsonAsync<Envelope<List<AdminDisputeBody>>>("/api/v1/admin/disputes");
        var dispute = disputes!.Data.First(d => d.TransactionId == tx.Id);

        _factory.Payments.RefundShouldFail = true;
        try
        {
            var response = await admin.PostAsJsonAsync($"/api/v1/admin/disputes/{dispute.Id}/resolve",
                new { decision = "refunded", note = "Décision favorable à l'acheteur." });

            response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        }
        finally
        {
            _factory.Payments.RefundShouldFail = false;
        }

        // La transaction ne doit surtout pas afficher un remboursement qui n'a pas eu lieu.
        var state = await _factory.Anonymous()
            .GetFromJsonAsync<Envelope<TransactionBody>>($"/api/v1/transactions/{tx.Token}");
        state!.Data.Status.Should().Be("dispute");
    }

    // ------------------------------------------------------------------ I-02

    [Fact]
    public async Task I02_La_liste_admin_des_litiges_porte_la_transaction_et_ses_deux_parties()
    {
        var tx = await PaidTransactionAsync("Litige contrat admin");
        var buyer = await BuyerAsync();
        var admin = await AdminAsync();
        await OpenDisputeAsync(buyer, tx.Id, "Article non conforme à l'annonce.");

        var disputes = await admin.GetFromJsonAsync<Envelope<List<AdminDisputeBody>>>("/api/v1/admin/disputes");
        var dispute = disputes!.Data.First(d => d.TransactionId == tx.Id);

        dispute.Ref.Should().Be($"SD-{tx.Id:D6}");
        dispute.TransactionTitle.Should().Be("Litige contrat admin");
        dispute.Amount.Should().Be("2000.00");
        dispute.VendorName.Should().NotBeNullOrEmpty();
        dispute.BuyerName.Should().NotBeNullOrEmpty();
        dispute.MessagesCount.Should().Be(1);
    }

    // ------------------------------------------------------------------ M-03

    [Fact]
    public async Task M03_Les_listes_admin_paginent_et_filtrent_cote_serveur()
    {
        var admin = await AdminAsync();

        var users = await admin.GetFromJsonAsync<Paged<UserBody>>("/api/v1/admin/users");
        users!.Meta.Total.Should().BeGreaterThan(0);

        var vendors = await admin.GetFromJsonAsync<Paged<UserBody>>("/api/v1/admin/users?role=vendor");
        vendors!.Data.Should().OnlyContain(u => u.Role == "vendor");

        var searched = await admin.GetFromJsonAsync<Paged<UserBody>>("/api/v1/admin/users?search=vendor@safedeal.com");
        searched!.Data.Should().ContainSingle().Which.Email.Should().Be("vendor@safedeal.com");
    }

    // ------------------------------------------------------------------ autorisations

    [Fact]
    public async Task Les_ecrans_d_administration_sont_reserves_aux_admins()
    {
        var vendor = await VendorAsync();

        (await vendor.GetAsync("/api/v1/admin/users")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await vendor.GetAsync("/api/v1/admin/disputes")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await vendor.GetAsync("/api/v1/dashboard/admin")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task M11_Les_pieces_d_identite_ne_sont_pas_servies_en_statique()
    {
        var response = await _factory.Anonymous().GetAsync("/uploads/identity/seed_document_front.png");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}

// ---------------------------------------------------------------------- formes de réponse

public record DisputeBody(int Id, string Category, string Description, string Status, List<EvidenceBody> Evidences);
public record EvidenceBody(int AuthorId, string AuthorName, string SubmittedBy, string Description, List<string> Files);
public record AdminDisputeBody(
    int Id, int TransactionId, string Ref, string TransactionTitle, string Amount,
    string? BuyerName, string VendorName, int MessagesCount);
public record Paged<T>(List<T> Data, PagedMeta Meta);
public record PagedMeta(int Current_page, int Last_page, int Total);
