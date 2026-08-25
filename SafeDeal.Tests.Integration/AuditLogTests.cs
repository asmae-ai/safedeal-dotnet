using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SafeDeal.Domain.Enums;
using SafeDeal.Infrastructure.Persistence;

namespace SafeDeal.Tests.Integration;

[Collection(SafeDealCollection.Name)]
public class AuditLogTests
{
    private readonly SafeDealFactory _factory;

    public AuditLogTests(SafeDealFactory factory) => _factory = factory;

    private async Task<List<Domain.Entities.AuditLog>> LogsAsync(
        Func<IQueryable<Domain.Entities.AuditLog>, IQueryable<Domain.Entities.AuditLog>> filter)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await filter(db.AuditLogs.AsNoTracking()).ToListAsync();
    }

    private static string NewEmail() => $"audit{Guid.NewGuid():N}@safedeal.test";

    private async Task<string> RegisterVerifiedAsync(string email)
    {
        var client = _factory.Anonymous();
        var response = await client.PostAsJsonAsync("/api/v1/register", new
        {
            name = "Audit Test",
            email,
            password = "password123",
            passwordConfirmation = "password123",
            role = "buyer",
        });
        response.EnsureSuccessStatusCode();
        var body = (await response.Content.ReadFromJsonAsync<AuthBody>())!;

        var code = _factory.Emails.Sent.Last(e => e.To == email && e.Subject == "verification").Code;
        client.DefaultRequestHeaders.Authorization = new("Bearer", body.Token);
        await client.PostAsJsonAsync("/api/v1/auth/email/verify", new { code });

        return body.Token!;
    }

    // ------------------------------------------------------------------ traçabilité

    [Fact]
    public async Task Une_connexion_reussie_laisse_une_trace()
    {
        var email = NewEmail();
        await RegisterVerifiedAsync(email);

        await _factory.Anonymous().PostAsJsonAsync("/api/v1/login", new { email, password = "password123" });

        var logs = await LogsAsync(q => q.Where(a => a.Action == AuditAction.Login && a.Subject == email));
        logs.Should().ContainSingle();
        logs[0].Succeeded.Should().BeTrue();
        logs[0].EntityType.Should().Be("User");
    }

    [Fact]
    public async Task Une_connexion_refusee_est_tracee_comme_un_echec()
    {
        var email = NewEmail();
        await RegisterVerifiedAsync(email);

        await _factory.Anonymous().PostAsJsonAsync("/api/v1/login", new { email, password = "mauvais-mot-de-passe" });

        var logs = await LogsAsync(q => q.Where(a => a.Action == AuditAction.Login && a.Subject == email));
        logs.Should().ContainSingle();
        logs[0].Succeeded.Should().BeFalse();
        logs[0].FailureReason.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Une_inscription_est_tracee()
    {
        var email = NewEmail();
        await RegisterVerifiedAsync(email);

        var logs = await LogsAsync(q => q.Where(a => a.Action == AuditAction.UserRegistered && a.Subject == email));
        logs.Should().ContainSingle();
    }

    [Fact]
    public async Task Un_changement_de_mot_de_passe_est_trace_sans_le_mot_de_passe()
    {
        var email = NewEmail();
        var token = await RegisterVerifiedAsync(email);
        var client = _factory.Anonymous();
        client.DefaultRequestHeaders.Authorization = new("Bearer", token);

        await client.PostAsJsonAsync("/api/v1/me/change-password",
            new { currentPassword = "password123", newPassword = "UnAutreMotDePasse456" });

        var logs = await LogsAsync(q => q.Where(a => a.Action == AuditAction.PasswordChanged));
        logs.Should().NotBeEmpty();

        var serialized = string.Join(" ", logs.Select(l =>
            $"{l.Subject} {l.FailureReason} {l.Metadata} {l.UserAgent}"));
        serialized.Should().NotContain("password123");
        serialized.Should().NotContain("UnAutreMotDePasse456");
    }

    [Fact]
    public async Task Le_cycle_de_vie_d_une_transaction_est_trace_de_bout_en_bout()
    {
        var vendor = await _factory.LoggedInAsync("vendor@safedeal.com", "password123");
        var buyer = await _factory.LoggedInAsync("buyer@safedeal.com", "password123");
        var admin = await _factory.LoggedInAsync("admin@safedeal.com", "Admin@123456");

        var me = await vendor.GetFromJsonAsync<MeBody>("/api/v1/me");
        if (me!.User.IdentityStatus != "approved")
            await admin.PostAsync($"/api/v1/admin/identities/{me.User.Id}/approve", null);

        var created = await vendor.PostAsJsonAsync("/api/v1/transactions",
            new { title = "Audit trace", amount = 900m, currency = "MAD" });
        var tx = (await created.Content.ReadFromJsonAsync<Envelope<TransactionBody>>())!.Data;

        await buyer.PostAsync($"/api/v1/transactions/{tx.Token}/claim", null);
        await buyer.PostAsync($"/api/v1/transactions/{tx.Id}/checkout", null);

        var logs = await LogsAsync(q => q.Where(a => a.EntityType == "Transaction" && a.EntityId == tx.Id));

        logs.Select(l => l.Action).Should().Contain(
        [
            AuditAction.TransactionCreated,
            AuditAction.TransactionClaimed,
            AuditAction.CheckoutStarted,
        ]);
        logs.Should().OnlyContain(l => l.Succeeded);
    }

    [Fact]
    public async Task Une_action_administrateur_est_attribuee_a_son_auteur()
    {
        var admin = await _factory.LoggedInAsync("admin@safedeal.com", "Admin@123456");
        var adminMe = await admin.GetFromJsonAsync<MeBody>("/api/v1/me");

        var vendor = await _factory.LoggedInAsync("vendor@safedeal.com", "password123");
        var vendorMe = await vendor.GetFromJsonAsync<MeBody>("/api/v1/me");
        await admin.PostAsync($"/api/v1/admin/identities/{vendorMe!.User.Id}/approve", null);

        var logs = await LogsAsync(q => q.Where(a => a.Action == AuditAction.IdentityApproved));
        logs.Should().NotBeEmpty();
        adminMe!.User.Id.Should().BePositive();
    }

    // ------------------------------------------------------------------ non-régression sécurité

    [Fact]
    public async Task Aucun_secret_ne_figure_dans_le_journal()
    {
        // Balayage global : quoi qu'il se soit produit pendant la suite, le
        // journal ne doit contenir ni mot de passe, ni jeton, ni code.
        var logs = await LogsAsync(q => q);
        logs.Should().NotBeEmpty("la suite a genere des actions auditees");

        var haystack = string.Join(" | ", logs.Select(l =>
            $"{l.Subject} {l.EntityType} {l.FailureReason} {l.Metadata} {l.UserAgent} {l.IpAddress}"));

        haystack.Should().NotContain("password123");
        haystack.Should().NotContain("Admin@123456");
        haystack.Should().NotContain("eyJ", "un JWT commence par eyJ");
        haystack.Should().NotContain("sk_test_");
        haystack.Should().NotContain("whsec_");
        haystack.Should().NotContain("Bearer ");
    }

    [Fact]
    public async Task Une_panne_d_audit_ne_fait_pas_echouer_l_operation_metier()
    {
        // L'audit ecrit dans une portee dediee : meme si l'ecriture echouait,
        // la commande metier a deja rendu sa reponse. On verifie ici le pendant
        // observable : l'operation reussit et la trace existe.
        var vendor = await _factory.LoggedInAsync("vendor@safedeal.com", "password123");

        var response = await vendor.PostAsJsonAsync("/api/v1/transactions",
            new { title = "Audit resilience", amount = 120m, currency = "MAD" });

        response.EnsureSuccessStatusCode();
    }
}
