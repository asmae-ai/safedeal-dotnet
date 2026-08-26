using FluentAssertions;
using SafeDeal.Domain.Entities;
using SafeDeal.Domain.Enums;

namespace SafeDeal.Tests.Unit.Domain;

/// <summary>
/// Le dossier d'identite conditionne le droit de creer une transaction :
/// son etat doit etre explicite a chaque etape, jamais deduit.
/// </summary>
public class IdentityVerificationTests
{
    private static IdentityVerification NewVerification() =>
        IdentityVerification.Create(
            userId: 42,
            documentType: "PASSPORT",
            documentFrontPath: "identity/42/front.jpg",
            selfiePath: "identity/42/selfie.jpg");

    [Fact]
    public void Un_dossier_soumis_part_en_attente_de_revue()
    {
        var verification = NewVerification();

        verification.Status.Should().Be(IdentityStatus.Pending);
        verification.RejectionReason.Should().BeNull();
        verification.SumsubApplicantId.Should().BeNull();
    }

    [Fact]
    public void Le_type_de_document_est_normalise()
    {
        // Le prestataire et l'ecran admin comparent des libelles : "PASSPORT" et
        // "passport" doivent designer le meme document.
        NewVerification().DocumentType.Should().Be("passport");
    }

    [Theory]
    [InlineData("", "front.jpg", "selfie.jpg")]
    [InlineData("passport", "", "selfie.jpg")]
    [InlineData("passport", "front.jpg", "")]
    [InlineData("passport", "front.jpg", "   ")]
    public void Un_dossier_incomplet_est_refuse(string type, string front, string selfie)
    {
        var act = () => IdentityVerification.Create(42, type, front, selfie);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Une_approbation_marque_le_dossier_vert()
    {
        var verification = NewVerification();

        verification.Approve();

        verification.Status.Should().Be(IdentityStatus.Approved);
        verification.RejectionReason.Should().BeNull("une approbation n'a pas de motif de refus");
    }

    [Fact]
    public void Un_refus_conserve_son_motif()
    {
        var verification = NewVerification();

        verification.Reject("Document illisible.");

        verification.Status.Should().Be(IdentityStatus.Rejected);
        verification.RejectionReason.Should().Be("Document illisible.");
    }

    [Fact]
    public void Un_refus_peut_etre_revu_apres_une_nouvelle_soumission()
    {
        // Un dossier refuse n'est pas un etat terminal : l'utilisateur renvoie
        // ses pieces et la revue suivante peut l'approuver.
        var verification = NewVerification();
        verification.Reject("Selfie flou.");

        verification.Approve();

        verification.Status.Should().Be(IdentityStatus.Approved);
    }

    [Fact]
    public void L_identifiant_du_prestataire_est_rattache_au_dossier()
    {
        // Sans ce rattachement, la decision qui revient par webhook ne peut plus
        // etre associee au dossier local.
        var verification = NewVerification();

        verification.SetSumsubApplicantId("applicant_42");

        verification.SumsubApplicantId.Should().Be("applicant_42");
    }

    [Fact]
    public void Chaque_decision_horodate_le_dossier()
    {
        var verification = NewVerification();
        var before = verification.UpdatedAt;

        Thread.Sleep(5);
        verification.Approve();

        verification.UpdatedAt.Should().BeAfter(before);
    }

    // ------------------------------------------------------- report sur le compte

    [Theory]
    [InlineData(IdentityStatus.NotSubmitted)]
    [InlineData(IdentityStatus.Pending)]
    [InlineData(IdentityStatus.Rejected)]
    public void Un_compte_non_approuve_reste_hors_du_parcours_vendeur(IdentityStatus status)
    {
        var user = User.Create("Vendeur", "vendeur@safedeal.test", "hash", UserRole.Vendor);

        user.UpdateIdentityStatus(status);

        user.IdentityStatus.Should().NotBe(IdentityStatus.Approved);
    }

    [Fact]
    public void Un_compte_approuve_porte_le_statut_vert()
    {
        var user = User.Create("Vendeur", "vendeur@safedeal.test", "hash", UserRole.Vendor);

        user.UpdateIdentityStatus(IdentityStatus.Approved);

        user.IdentityStatus.Should().Be(IdentityStatus.Approved);
    }
}
