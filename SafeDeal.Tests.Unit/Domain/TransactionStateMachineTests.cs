using FluentAssertions;
using SafeDeal.Domain.Entities;
using SafeDeal.Domain.Enums;
using SafeDeal.Domain.Exceptions;

namespace SafeDeal.Tests.Unit.Domain;

/// <summary>
/// Le cycle de vie d'une transaction porte la garantie centrale du produit :
/// l'argent ne bouge que par une transition autorisée.
/// </summary>
public class TransactionStateMachineTests
{
    private static Transaction NewTransaction() =>
        Transaction.Create("Casque audio", 1500m, "MAD", vendorId: 1);

    private static Transaction Paid()
    {
        var t = NewTransaction();
        t.Claim(buyerId: 2);
        t.Transition(TransactionStatus.PaymentReceived);
        return t;
    }

    [Fact]
    public void Une_transaction_naissante_attend_le_paiement()
    {
        NewTransaction().Status.Should().Be(TransactionStatus.PendingPayment);
    }

    [Fact]
    public void Le_token_securise_est_unique_par_transaction()
    {
        NewTransaction().SecureToken.Should().NotBe(NewTransaction().SecureToken);
    }

    [Theory]
    [InlineData(TransactionStatus.InShipping)]
    [InlineData(TransactionStatus.Delivered)]
    [InlineData(TransactionStatus.Closed)]
    public void Une_transaction_impayee_ne_peut_pas_avancer(TransactionStatus cible)
    {
        var acte = () => NewTransaction().Transition(cible);
        acte.Should().Throw<InvalidTransitionException>();
    }

    [Fact]
    public void Un_paiement_encaisse_ouvre_l_expedition()
    {
        var t = Paid();
        t.Transition(TransactionStatus.InShipping);
        t.Status.Should().Be(TransactionStatus.InShipping);
    }

    [Fact]
    public void Un_acheteur_qui_a_paye_peut_ouvrir_un_litige_sans_attendre_l_expedition()
    {
        // Regression C-04 : le vendeur qui n'expedie jamais laissait l'acheteur
        // sans recours, la transition PaymentReceived -> Dispute etant interdite.
        var t = Paid();
        t.Transition(TransactionStatus.Dispute);
        t.Status.Should().Be(TransactionStatus.Dispute);
    }

    [Theory]
    [InlineData(TransactionStatus.Closed)]
    [InlineData(TransactionStatus.Cancelled)]
    [InlineData(TransactionStatus.Refunded)]
    [InlineData(TransactionStatus.Resolved)]
    public void Un_etat_terminal_est_definitif(TransactionStatus terminal)
    {
        var t = Paid();
        if (terminal is TransactionStatus.Refunded or TransactionStatus.Resolved)
            t.Transition(TransactionStatus.Dispute);
        else if (terminal == TransactionStatus.Closed)
        {
            t.Transition(TransactionStatus.InShipping);
            t.Transition(TransactionStatus.Delivered);
        }

        t.Transition(terminal);

        var acte = () => t.Transition(TransactionStatus.InShipping);
        acte.Should().Throw<InvalidTransitionException>(
            "une transaction cloturee ne peut plus etre modifiee");
    }

    [Fact]
    public void Un_second_paiement_est_refuse()
    {
        var t = Paid();
        var acte = () => t.Transition(TransactionStatus.PaymentReceived);
        acte.Should().Throw<InvalidTransitionException>();
    }

    [Fact]
    public void Une_transition_invalide_est_une_erreur_de_domaine_donc_une_422()
    {
        // Regression C-09 : InvalidTransitionException heritait de DomainException,
        // que le middleware ne couvrait pas ; chaque regle metier sortait en 500.
        var acte = () => NewTransaction().Transition(TransactionStatus.Closed);
        acte.Should().Throw<InvalidTransitionException>().And.Should().BeAssignableTo<DomainException>();
    }

    [Fact]
    public void Chaque_transition_laisse_une_trace_datee()
    {
        var t = Paid();
        t.Transition(TransactionStatus.InShipping, "Colis remis au transporteur.");

        t.Logs.Should().HaveCount(2);
        t.Logs.Last().Note.Should().Be("Colis remis au transporteur.");
    }

    [Fact]
    public void Un_vendeur_ne_peut_pas_acheter_sa_propre_transaction()
    {
        var acte = () => NewTransaction().Claim(buyerId: 1);
        acte.Should().Throw<BusinessRuleException>();
    }

    [Fact]
    public void Une_transaction_deja_reservee_ne_change_pas_d_acheteur()
    {
        var t = NewTransaction();
        t.Claim(buyerId: 2);

        var acte = () => t.Claim(buyerId: 3);
        acte.Should().Throw<BusinessRuleException>();
    }

    [Fact]
    public void L_expedition_exige_un_numero_de_suivi()
    {
        var acte = () => Paid().SetShipping("", "CTM");
        acte.Should().Throw<ArgumentException>();
    }
}
