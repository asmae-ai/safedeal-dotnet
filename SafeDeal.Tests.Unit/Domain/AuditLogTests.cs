using FluentAssertions;
using SafeDeal.Application.Common.Audit;
using SafeDeal.Domain.Entities;
using SafeDeal.Domain.Enums;

namespace SafeDeal.Tests.Unit.Domain;

public class AuditLogTests
{
    [Fact]
    public void Une_trace_retient_l_auteur_l_action_et_la_cible()
    {
        var log = AuditLog.Record(
            AuditAction.TransactionShipped,
            userId: 7,
            entityType: "Transaction",
            entityId: 42,
            ipAddress: "192.0.2.10");

        log.Action.Should().Be(AuditAction.TransactionShipped);
        log.UserId.Should().Be(7);
        log.EntityId.Should().Be(42);
        log.Succeeded.Should().BeTrue();
        log.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Un_echec_conserve_son_motif()
    {
        var log = AuditLog.Record(
            AuditAction.Login,
            subject: "inconnu@safedeal.com",
            succeeded: false,
            failureReason: "Invalid credentials.");

        log.Succeeded.Should().BeFalse();
        log.FailureReason.Should().Be("Invalid credentials.");
        log.UserId.Should().BeNull("une tentative echouee n'identifie pas forcement un compte");
    }

    [Fact]
    public void Les_champs_trop_longs_sont_tronques_plutot_que_de_faire_echouer_l_ecriture()
    {
        var log = AuditLog.Record(
            AuditAction.Login,
            userAgent: new string('a', 5000),
            failureReason: new string('b', 5000));

        log.UserAgent!.Length.Should().Be(512);
        log.FailureReason!.Length.Should().Be(500);
    }

    [Fact]
    public void Les_champs_vides_sont_normalises_a_null()
    {
        var log = AuditLog.Record(AuditAction.Logout, subject: "   ", ipAddress: "");

        log.Subject.Should().BeNull();
        log.IpAddress.Should().BeNull();
    }
}

public class AuditRedactionTests
{
    [Theory]
    [InlineData("password")]
    [InlineData("Password")]
    [InlineData("currentPassword")]
    [InlineData("refreshToken")]
    [InlineData("jwt")]
    [InlineData("otpCode")]
    [InlineData("stripe_secret")]
    [InlineData("Authorization")]
    [InlineData("apiKey")]
    [InlineData("cardNumber")]
    [InlineData("X-Payload-Signature")]
    public void Une_cle_sensible_est_reconnue(string key)
    {
        AuditRedaction.IsSensitiveKey(key).Should().BeTrue();
    }

    [Theory]
    [InlineData("transactionId")]
    [InlineData("amount")]
    [InlineData("status")]
    [InlineData("carrier")]
    public void Une_cle_metier_ordinaire_passe(string key)
    {
        AuditRedaction.IsSensitiveKey(key).Should().BeFalse();
    }

    [Fact]
    public void Une_valeur_sensible_passee_par_erreur_est_masquee_avant_ecriture()
    {
        var json = AuditRedaction.Serialize(new Dictionary<string, object?>
        {
            ["amount"] = "1500.00",
            ["password"] = "SuperSecret123",
            ["refreshToken"] = "abcdef0123456789",
        });

        json.Should().NotBeNull();
        json.Should().Contain("1500.00");
        json.Should().NotContain("SuperSecret123");
        json.Should().NotContain("abcdef0123456789");
        json.Should().Contain(AuditRedaction.Placeholder);
    }

    [Fact]
    public void Des_metadonnees_absentes_n_ecrivent_rien()
    {
        AuditRedaction.Serialize(null).Should().BeNull();
        AuditRedaction.Serialize(new Dictionary<string, object?>()).Should().BeNull();
    }
}
