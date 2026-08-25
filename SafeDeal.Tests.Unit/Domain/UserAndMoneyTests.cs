using FluentAssertions;
using SafeDeal.Domain.Entities;
using SafeDeal.Domain.Enums;
using SafeDeal.Domain.ValueObjects;

namespace SafeDeal.Tests.Unit.Domain;

public class MoneyTests
{
    [Fact]
    public void Un_montant_est_arrondi_au_centime()
    {
        new Money(12.3456m, "mad").Amount.Should().Be(12.35m);
    }

    [Fact]
    public void La_devise_est_normalisee_en_majuscules()
    {
        new Money(10m, "mad").Currency.Should().Be("MAD");
    }

    [Fact]
    public void Un_montant_negatif_est_refuse()
    {
        var acte = () => new Money(-1m, "MAD");
        acte.Should().Throw<ArgumentException>();
    }
}

public class UserTests
{
    private static User NewVendor() =>
        User.Create("Vendor Test", "VENDOR@Safedeal.com ", "hash", UserRole.Vendor, " +212600000000 ");

    [Fact]
    public void L_email_est_normalise_et_le_telephone_conserve()
    {
        // Regression I-11 : le telephone saisi a l'inscription etait ignore.
        var user = NewVendor();
        user.Email.Should().Be("vendor@safedeal.com");
        user.Phone.Should().Be("+212600000000");
    }

    [Fact]
    public void Un_compte_neuf_n_a_ni_email_verifie_ni_identite_soumise()
    {
        var user = NewVendor();
        user.IsEmailVerified.Should().BeFalse();
        user.IdentityStatus.Should().Be(IdentityStatus.NotSubmitted);
        user.TwoFactorEnabled.Should().BeFalse();
    }

    [Fact]
    public void La_reputation_monte_a_chaque_transaction_reussie_et_plafonne_a_cinq()
    {
        // Regression A-04 : le score etait affiche partout et jamais alimente.
        var user = NewVendor();
        user.ReputationScore.Should().Be(0m);

        for (var i = 0; i < 100; i++) user.RegisterSuccessfulTransaction();

        user.ReputationScore.Should().Be(5m);
    }

    [Fact]
    public void La_reputation_baisse_sur_litige_perdu_sans_passer_sous_zero()
    {
        var user = NewVendor();
        user.RegisterSuccessfulTransaction();
        user.RegisterFailedTransaction();
        user.RegisterFailedTransaction();

        user.ReputationScore.Should().Be(0m);
    }

    [Fact]
    public void Le_mot_de_passe_est_verifiable_apres_changement()
    {
        var user = NewVendor();
        user.ChangePassword("NouveauMotDePasse1");

        user.VerifyPassword("NouveauMotDePasse1").Should().BeTrue();
        user.VerifyPassword("autre").Should().BeFalse();
    }

    [Fact]
    public void La_mise_a_jour_du_profil_ignore_les_champs_vides()
    {
        var user = NewVendor();
        user.UpdateProfile(name: "  ", phone: null);

        user.Name.Should().Be("Vendor Test");
        user.Phone.Should().Be("+212600000000");
    }
}
