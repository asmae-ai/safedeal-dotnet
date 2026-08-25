using FluentAssertions;
using SafeDeal.Domain.Entities;
using SafeDeal.Domain.Enums;
using SafeDeal.Domain.Exceptions;

namespace SafeDeal.Tests.Unit.Domain;

public class DisputeTests
{
    private static Dispute NewDispute() =>
        Dispute.Create(transactionId: 1, openedByUserId: 2, "damaged", "Le colis est arrive casse.");

    [Fact]
    public void Un_litige_naissant_est_ouvert()
    {
        NewDispute().Status.Should().Be(DisputeStatus.Open);
    }

    [Fact]
    public void La_reclamation_initiale_porte_son_auteur()
    {
        // Regression I-12 : l'auteur des preuves etait devine par une alternance
        // i % 2, ce qui attribuait la moitie des pieces a la mauvaise partie.
        var dispute = NewDispute();
        dispute.AddMessage(authorUserId: 2, "Le colis est arrive casse.", ["preuve.png"]);

        dispute.Messages.Should().ContainSingle();
        dispute.Messages.First().AuthorUserId.Should().Be(2);
        dispute.Messages.First().Files.Should().ContainSingle();
    }

    [Fact]
    public void La_reponse_de_l_autre_partie_place_le_litige_en_examen()
    {
        var dispute = NewDispute();
        dispute.AddMessage(2, "Reclamation de l'acheteur.");
        dispute.AddMessage(1, "Version du vendeur.");

        dispute.Status.Should().Be(DisputeStatus.UnderReview);
        dispute.Messages.Should().HaveCount(2);
    }

    [Fact]
    public void Les_pieces_jointes_alimentent_le_dossier_de_preuves()
    {
        var dispute = NewDispute();
        dispute.AddMessage(2, "Photos du colis.", ["a.png", "b.png"]);

        dispute.EvidenceFiles.Should().HaveCount(2);
    }

    [Fact]
    public void Un_litige_tranche_n_accepte_plus_d_echange()
    {
        var dispute = NewDispute();
        dispute.Resolve("Remboursement accorde a l'acheteur.");

        var acte = () => dispute.AddMessage(1, "Trop tard.");
        acte.Should().Throw<BusinessRuleException>();
    }

    [Fact]
    public void Une_decision_conserve_sa_motivation()
    {
        var dispute = NewDispute();
        dispute.Resolve("Preuves de l'acheteur retenues.");

        dispute.Status.Should().Be(DisputeStatus.Resolved);
        dispute.ResolutionNote.Should().Be("Preuves de l'acheteur retenues.");
    }

    [Fact]
    public void Un_litige_exige_une_categorie_et_une_description()
    {
        var sansCategorie = () => Dispute.Create(1, 2, "", "Description valable.");
        sansCategorie.Should().Throw<ArgumentException>();

        var sansDescription = () => Dispute.Create(1, 2, "damaged", "   ");
        sansDescription.Should().Throw<ArgumentException>();
    }
}
