using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SafeDeal.Domain.Enums;
using SafeDeal.Infrastructure.Persistence;

namespace SafeDeal.Tests.Integration;

/// <summary>
/// Verification d'identite de bout en bout : ce que le prestataire renvoie
/// decide qui a le droit de creer une transaction, donc chaque decision doit
/// etre appliquee une fois, et une seule.
/// </summary>
[Collection(SafeDealCollection.Name)]
public class IdentityVerificationTests
{
    private readonly SafeDealFactory _factory;

    public IdentityVerificationTests(SafeDealFactory factory) => _factory = factory;

    private sealed record AuthBody(string Token, RegisteredUser User);
    private sealed record RegisteredUser(int Id, string Email, string IdentityStatus);
    private sealed record StatusBody(string Status, string? SubmittedAt);

    private static string NewEmail() => $"kyc{Guid.NewGuid():N}@safedeal.test";

    /// <summary>Cree un vendeur verifie par e-mail et depose son dossier d'identite.</summary>
    private async Task<(HttpClient Client, int UserId)> VendorWithPendingKycAsync()
    {
        var email = NewEmail();
        var client = _factory.Anonymous();

        var registered = await client.PostAsJsonAsync("/api/v1/register", new
        {
            name = "Vendeur KYC",
            email,
            password = "password123",
            passwordConfirmation = "password123",
            role = "vendor",
        });
        registered.EnsureSuccessStatusCode();
        var body = (await registered.Content.ReadFromJsonAsync<AuthBody>())!;

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body.Token);

        var code = _factory.Emails.Sent.Last(e => e.To == email && e.Subject == "verification").Code;
        await client.PostAsJsonAsync("/api/v1/auth/email/verify", new { code });

        var submitted = await client.PostAsync("/api/v1/verify-identity", IdentityForm());
        submitted.EnsureSuccessStatusCode();

        return (client, body.User.Id);
    }

    private static MultipartFormDataContent IdentityForm()
    {
        var front = new ByteArrayContent(Encoding.UTF8.GetBytes("front"));
        front.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        var selfie = new ByteArrayContent(Encoding.UTF8.GetBytes("selfie"));
        selfie.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");

        return new MultipartFormDataContent
        {
            { new StringContent("passport"), "documentType" },
            { front, "documentFront", "front.jpg" },
            { selfie, "selfie", "selfie.jpg" },
        };
    }

    private Task<HttpResponseMessage> SumsubWebhookAsync(int userId, string? answer, string? comment = null)
    {
        var reviewResult = answer is null
            ? "null"
            : $$"""{"reviewAnswer":"{{answer}}","moderationComment":{{(comment is null ? "null" : $"\"{comment}\"")}}}""";

        var payload = $$"""
            {"applicantId":"applicant_{{userId}}","externalUserId":"{{userId}}","type":"applicantReviewed","reviewResult":{{reviewResult}}}
            """;

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/webhooks/sumsub")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
        request.Headers.Add("X-Payload-Digest", "digest-de-test");

        return _factory.Anonymous().SendAsync(request);
    }

    private async Task<IdentityStatus> StoredStatusAsync(int userId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return (await db.IdentityVerifications.AsNoTracking()
            .SingleAsync(v => v.UserId == userId)).Status;
    }

    private async Task<IdentityStatus> AccountStatusAsync(int userId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return (await db.Users.AsNoTracking().SingleAsync(u => u.Id == userId)).IdentityStatus;
    }

    // ------------------------------------------------------------------ soumission

    [Fact]
    public async Task Un_dossier_depose_reste_en_attente_de_decision()
    {
        var (client, userId) = await VendorWithPendingKycAsync();

        var status = await client.GetFromJsonAsync<StatusBody>("/api/v1/verify-identity/status");

        status!.Status.Should().Be("pending");
        (await StoredStatusAsync(userId)).Should().Be(IdentityStatus.Pending);
    }

    [Fact]
    public async Task Un_second_depot_pendant_l_examen_est_refuse()
    {
        // Deux dossiers ouverts pour un meme compte rendraient la decision du
        // prestataire ambigue.
        var (client, _) = await VendorWithPendingKycAsync();

        var second = await client.PostAsync("/api/v1/verify-identity", IdentityForm());

        second.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Un_acheteur_ne_peut_pas_deposer_de_dossier_vendeur()
    {
        var buyer = await _factory.LoggedInAsync("buyer@safedeal.com", "password123");

        var response = await buyer.PostAsync("/api/v1/verify-identity", IdentityForm());

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Un_fichier_au_mauvais_format_est_refuse()
    {
        var (client, _) = await VendorWithPendingKycAsync();
        var form = new MultipartFormDataContent
        {
            { new StringContent("passport"), "documentType" },
            { new ByteArrayContent(Encoding.UTF8.GetBytes("front")), "documentFront", "front.exe" },
            { new ByteArrayContent(Encoding.UTF8.GetBytes("selfie")), "selfie", "selfie.jpg" },
        };

        var response = await client.PostAsync("/api/v1/verify-identity", form);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    // --------------------------------------------------------------------- webhook

    [Fact]
    public async Task Une_decision_verte_approuve_le_dossier_et_le_compte()
    {
        var (_, userId) = await VendorWithPendingKycAsync();

        var response = await SumsubWebhookAsync(userId, "GREEN");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await StoredStatusAsync(userId)).Should().Be(IdentityStatus.Approved);
        (await AccountStatusAsync(userId)).Should().Be(IdentityStatus.Approved);
    }

    [Fact]
    public async Task Une_decision_rouge_refuse_le_dossier_et_retient_le_motif()
    {
        var (_, userId) = await VendorWithPendingKycAsync();

        await SumsubWebhookAsync(userId, "RED", "Document expire.");

        (await StoredStatusAsync(userId)).Should().Be(IdentityStatus.Rejected);
        (await AccountStatusAsync(userId)).Should().Be(IdentityStatus.Rejected);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var verification = await db.IdentityVerifications.AsNoTracking().SingleAsync(v => v.UserId == userId);
        verification.RejectionReason.Should().Be("Document expire.");
    }

    [Fact]
    public async Task Un_evenement_sans_decision_laisse_le_dossier_en_attente()
    {
        // Sumsub emet plusieurs evenements par dossier ; seule la revue finale
        // porte une reponse.
        var (_, userId) = await VendorWithPendingKycAsync();

        var response = await SumsubWebhookAsync(userId, answer: null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await StoredStatusAsync(userId)).Should().Be(IdentityStatus.Pending);
    }

    [Fact]
    public async Task Un_condense_invalide_est_rejete_sans_toucher_au_dossier()
    {
        var (_, userId) = await VendorWithPendingKycAsync();
        _factory.Identity.SignatureIsValid = false;
        try
        {
            var response = await SumsubWebhookAsync(userId, "GREEN");

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await StoredStatusAsync(userId)).Should().Be(IdentityStatus.Pending);
        }
        finally
        {
            _factory.Identity.SignatureIsValid = true;
        }
    }

    [Fact]
    public async Task Un_rejeu_ne_remplace_pas_une_decision_deja_appliquee()
    {
        // Sumsub reemet tant qu'il n'a pas de 2xx : un rejeu portant une autre
        // reponse ne doit pas reecrire la decision d'origine.
        var (_, userId) = await VendorWithPendingKycAsync();
        await SumsubWebhookAsync(userId, "GREEN");

        var replay = await SumsubWebhookAsync(userId, "RED", "Contradiction.");

        replay.StatusCode.Should().Be(HttpStatusCode.OK);
        (await StoredStatusAsync(userId)).Should().Be(IdentityStatus.Approved);
        (await AccountStatusAsync(userId)).Should().Be(IdentityStatus.Approved);
    }

    [Fact]
    public async Task Une_decision_pour_un_dossier_inconnu_est_acquittee_sans_effet()
    {
        // Un 500 ferait boucler le prestataire sur un evenement qu'aucun rejeu
        // ne rendra applicable.
        var response = await SumsubWebhookAsync(userId: 999_999, answer: "GREEN");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Un_corps_illisible_est_rejete_en_400()
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/webhooks/sumsub")
        {
            Content = new StringContent("{ceci n'est pas du json", Encoding.UTF8, "application/json")
        };
        request.Headers.Add("X-Payload-Digest", "digest-de-test");

        var response = await _factory.Anonymous().SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ------------------------------------------------------------------ retombees

    [Fact]
    public async Task Un_vendeur_approuve_par_le_prestataire_peut_creer_une_transaction()
    {
        var (client, userId) = await VendorWithPendingKycAsync();
        await SumsubWebhookAsync(userId, "GREEN");

        var created = await client.PostAsJsonAsync("/api/v1/transactions",
            new { title = "Guitare", amount = 1500m, currency = "MAD" });

        created.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Un_vendeur_refuse_par_le_prestataire_reste_hors_du_parcours()
    {
        var (client, userId) = await VendorWithPendingKycAsync();
        await SumsubWebhookAsync(userId, "RED", "Document expire.");

        var created = await client.PostAsJsonAsync("/api/v1/transactions",
            new { title = "Guitare", amount = 1500m, currency = "MAD" });

        created.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
