using FluentAssertions;
using SafeDeal.Application.Admin.Commands.RejectIdentity;
using SafeDeal.Application.Auth.Commands.ChangePassword;
using SafeDeal.Application.Auth.Commands.RefreshToken;
using SafeDeal.Application.Auth.Commands.ResetPassword;
using SafeDeal.Application.Auth.Commands.UpdateProfile;
using SafeDeal.Application.Auth.Commands.VerifyTwoFactor;
using SafeDeal.Application.Common.Models;
using SafeDeal.Application.Disputes.Commands.ResolveDispute;
using SafeDeal.Application.Disputes.Commands.SubmitEvidence;
using SafeDeal.Application.Transactions.Commands.CreateTransaction;
using SafeDeal.Application.Transactions.Queries.GetTransactions;

namespace SafeDeal.Tests.Unit.Validation;

public class PasswordValidationTests
{
    private readonly ChangePasswordCommandValidator _validator = new();

    [Fact]
    public void Un_changement_de_mot_de_passe_valide_passe()
    {
        _validator.Validate(new ChangePasswordCommand(1, "ancien123", "NouveauPass456"))
            .IsValid.Should().BeTrue();
    }

    [Fact]
    public void Un_nouveau_mot_de_passe_trop_court_est_refuse()
    {
        var result = _validator.Validate(new ChangePasswordCommand(1, "ancien123", "court"));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "NewPassword");
    }

    [Fact]
    public void Reutiliser_le_meme_mot_de_passe_est_refuse()
    {
        // Un changement qui ne change rien laisse croire a une rotation effectuee.
        var result = _validator.Validate(new ChangePasswordCommand(1, "identique123", "identique123"));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("differ"));
    }

    [Fact]
    public void Un_mot_de_passe_actuel_vide_est_refuse()
    {
        _validator.Validate(new ChangePasswordCommand(1, "", "NouveauPass456"))
            .IsValid.Should().BeFalse();
    }
}

public class ResetPasswordValidationTests
{
    private readonly ResetPasswordCommandValidator _validator = new();

    [Fact]
    public void Une_reinitialisation_coherente_passe()
    {
        _validator.Validate(new ResetPasswordCommand("a@b.com", "jeton", "MotDePasse1", "MotDePasse1"))
            .IsValid.Should().BeTrue();
    }

    [Fact]
    public void Une_confirmation_differente_est_refusee()
    {
        _validator.Validate(new ResetPasswordCommand("a@b.com", "jeton", "MotDePasse1", "Autre1234"))
            .IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData("pas-un-email")]
    [InlineData("")]
    public void Un_email_invalide_est_refuse(string email)
    {
        _validator.Validate(new ResetPasswordCommand(email, "jeton", "MotDePasse1", "MotDePasse1"))
            .IsValid.Should().BeFalse();
    }
}

public class OtpValidationTests
{
    private readonly VerifyTwoFactorCommandValidator _validator = new();

    [Fact]
    public void Un_code_a_six_chiffres_passe()
    {
        _validator.Validate(new VerifyTwoFactorCommand("a@b.com", "123456")).IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("12345")]
    [InlineData("1234567")]
    [InlineData("abcdef")]
    [InlineData("12 45 6")]
    [InlineData("")]
    public void Un_code_mal_forme_est_refuse(string code)
    {
        _validator.Validate(new VerifyTwoFactorCommand("a@b.com", code)).IsValid.Should().BeFalse();
    }
}

public class TransactionValidationTests
{
    private readonly CreateTransactionCommandValidator _validator = new();

    [Fact]
    public void Une_transaction_valide_passe()
    {
        _validator.Validate(new CreateTransactionCommand(1, "Casque", 1500m, "MAD"))
            .IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-0.01)]
    public void Un_montant_nul_ou_negatif_est_refuse(decimal amount)
    {
        _validator.Validate(new CreateTransactionCommand(1, "Casque", amount, "MAD"))
            .IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData("MA")]
    [InlineData("MADX")]
    [InlineData("")]
    public void Une_devise_mal_formee_est_refusee(string currency)
    {
        _validator.Validate(new CreateTransactionCommand(1, "Casque", 100m, currency))
            .IsValid.Should().BeFalse();
    }

    [Fact]
    public void Un_titre_vide_est_refuse()
    {
        _validator.Validate(new CreateTransactionCommand(1, "", 100m, "MAD"))
            .IsValid.Should().BeFalse();
    }
}

public class DisputeValidationTests
{
    [Theory]
    [InlineData("resolved", true)]
    [InlineData("refunded", true)]
    [InlineData("REFUNDED", false)]
    [InlineData("annule", false)]
    [InlineData("", false)]
    public void Seules_deux_decisions_sont_recevables(string decision, bool expected)
    {
        new ResolveDisputeCommandValidator()
            .Validate(new ResolveDisputeCommand(1, decision, "note"))
            .IsValid.Should().Be(expected);
    }

    [Fact]
    public void Un_identifiant_de_litige_invalide_est_refuse()
    {
        new ResolveDisputeCommandValidator()
            .Validate(new ResolveDisputeCommand(0, "resolved", "note"))
            .IsValid.Should().BeFalse();
    }

    [Fact]
    public void Une_reponse_trop_courte_est_refusee()
    {
        new SubmitEvidenceCommandValidator()
            .Validate(new SubmitEvidenceCommand(1, 2, "court", []))
            .IsValid.Should().BeFalse();
    }

    [Fact]
    public void Une_reponse_argumentee_passe()
    {
        new SubmitEvidenceCommandValidator()
            .Validate(new SubmitEvidenceCommand(1, 2, "Le colis a ete remis intact au transporteur.", []))
            .IsValid.Should().BeTrue();
    }
}

public class AdminValidationTests
{
    [Fact]
    public void Un_rejet_sans_motif_est_refuse()
    {
        // Le motif est affiche a l'utilisateur : sans lui, il ne peut pas corriger.
        new RejectIdentityCommandValidator()
            .Validate(new RejectIdentityCommand(1, ""))
            .IsValid.Should().BeFalse();
    }

    [Fact]
    public void Un_rejet_motive_passe()
    {
        new RejectIdentityCommandValidator()
            .Validate(new RejectIdentityCommand(1, "Document illisible."))
            .IsValid.Should().BeTrue();
    }
}

public class ProfileValidationTests
{
    private readonly UpdateProfileCommandValidator _validator = new();

    [Fact]
    public void Une_mise_a_jour_partielle_reste_acceptee()
    {
        // Les deux champs sont optionnels : ne rien fournir n'est pas une erreur.
        _validator.Validate(new UpdateProfileCommand(1, null, null)).IsValid.Should().BeTrue();
        _validator.Validate(new UpdateProfileCommand(1, "Nouveau Nom", null)).IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("12345")]
    [InlineData("+2126001122334455667788")]
    public void Un_telephone_mal_forme_est_refuse(string phone)
    {
        _validator.Validate(new UpdateProfileCommand(1, null, phone)).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Un_telephone_valide_passe()
    {
        _validator.Validate(new UpdateProfileCommand(1, null, "+212600112233")).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Un_jeton_de_rafraichissement_vide_est_refuse()
    {
        new RefreshTokenCommandValidator().Validate(new RefreshTokenCommand("")).IsValid.Should().BeFalse();
    }
}

public class PagingTests
{
    [Theory]
    [InlineData(0, 1)]
    [InlineData(-5, 1)]
    [InlineData(1, 1)]
    [InlineData(42, 42)]
    public void Une_page_hors_bornes_est_ramenee_a_la_premiere(int input, int expected)
    {
        // Ramener plutot que rejeter : le contrat existant ne renvoyait pas
        // d'erreur pour une page invalide, il partait en erreur serveur.
        Paging.NormalizePage(input).Should().Be(expected);
    }

    [Theory]
    [InlineData(0, Paging.DefaultPageSize)]
    [InlineData(-1, Paging.DefaultPageSize)]
    [InlineData(50, 50)]
    [InlineData(1000, Paging.MaxPageSize)]
    public void La_taille_de_page_est_plafonnee(int input, int expected)
    {
        Paging.NormalizePageSize(input).Should().Be(expected);
    }

    [Fact]
    public void Les_bornes_sont_appliquees_par_la_requete_elle_meme()
    {
        var query = new GetTransactionsQuery(UserId: 1, Page: -3, PageSize: 9999);

        query.SafePage.Should().Be(1);
        query.SafePageSize.Should().Be(Paging.MaxPageSize);
        query.Page.Should().Be(-3, "la valeur d'origine reste lisible pour le diagnostic");
    }
}
