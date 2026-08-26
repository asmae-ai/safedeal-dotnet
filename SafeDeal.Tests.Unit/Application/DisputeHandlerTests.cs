using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SafeDeal.Application.Common.Exceptions;
using SafeDeal.Application.Common.Interfaces;
using SafeDeal.Application.Disputes.Commands.OpenDispute;
using SafeDeal.Application.Disputes.Commands.ResolveDispute;
using SafeDeal.Domain.Entities;
using SafeDeal.Domain.Enums;
using SafeDeal.Domain.Events;
using SafeDeal.Domain.Exceptions;
using SafeDeal.Domain.Interfaces.Repositories;
using SafeDeal.Domain.Interfaces.Services;

namespace SafeDeal.Tests.Unit.Application;

/// <summary>
/// Le litige est le seul chemin par lequel des fonds deja en sequestre peuvent
/// repartir chez l'acheteur : qui peut l'ouvrir, et ce qu'une resolution
/// declenche, se verifient cas par cas.
/// </summary>
public class DisputeHandlerTests
{
    private const int VendorId = 1;
    private const int BuyerId = 2;
    private const int OutsiderId = 99;

    private readonly Mock<IDisputeRepository> _disputes = new();
    private readonly Mock<ITransactionRepository> _transactions = new();
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IPaymentService> _payments = new();
    private readonly Mock<IApplicationDbContext> _context = new();
    private readonly Mock<IPublisher> _publisher = new();

    public DisputeHandlerTests()
    {
        // La commande enveloppe ses ecritures dans une transaction base ;
        // la doublure se contente d'executer l'operation transmise.
        _context.Setup(c => c.ExecuteInTransactionAsync(It.IsAny<Func<Task>>(), It.IsAny<CancellationToken>()))
            .Returns((Func<Task> operation, CancellationToken _) => operation());

        _users.Setup(r => r.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((int id, CancellationToken _) =>
                User.Create($"User {id}", $"user{id}@safedeal.test", "hash", UserRole.Buyer));
    }

    private OpenDisputeCommandHandler OpenHandler() => new(
        _disputes.Object, _transactions.Object, _users.Object, _context.Object, _publisher.Object);

    private ResolveDisputeCommandHandler ResolveHandler() => new(
        _disputes.Object, _transactions.Object, _payments.Object, _context.Object, _publisher.Object,
        NullLogger<ResolveDisputeCommandHandler>.Instance);

    private static Transaction PaidTransaction(string? paymentIntentId = "pi_test")
    {
        var transaction = Transaction.Create("Appareil photo", 3000m, "MAD", VendorId);
        transaction.Claim(BuyerId);
        if (paymentIntentId is not null) transaction.SetStripePaymentIntent(paymentIntentId);
        transaction.Transition(TransactionStatus.PaymentReceived);
        return transaction;
    }

    private static Dispute OpenDispute() =>
        Dispute.Create(transactionId: 1, openedByUserId: BuyerId, "not_received", "Colis jamais recu.");

    private void GivenTransaction(Transaction transaction) =>
        _transactions.Setup(r => r.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(transaction);

    private void GivenDispute(Dispute? dispute) =>
        _disputes.Setup(r => r.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(dispute);

    private void GivenExistingDisputeOnTransaction(Dispute? dispute) =>
        _disputes.Setup(r => r.GetByTransactionIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(dispute);

    // ---------------------------------------------------------------- ouverture

    [Fact]
    public async Task Une_partie_a_la_transaction_peut_ouvrir_un_litige()
    {
        var transaction = PaidTransaction();
        GivenTransaction(transaction);
        GivenExistingDisputeOnTransaction(null);

        await OpenHandler().Handle(
            new OpenDisputeCommand(1, BuyerId, "not_received", "Colis jamais recu.", []), default);

        // L'ouverture gele les fonds : la transaction quitte l'etat payable.
        transaction.Status.Should().Be(TransactionStatus.Dispute);
        _disputes.Verify(r => r.AddAsync(It.IsAny<Dispute>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Un_tiers_ne_peut_pas_ouvrir_de_litige()
    {
        GivenTransaction(PaidTransaction());
        GivenExistingDisputeOnTransaction(null);

        var act = () => OpenHandler().Handle(
            new OpenDisputeCommand(1, OutsiderId, "not_received", "Je conteste.", []), default);

        await act.Should().ThrowAsync<ForbiddenException>();
        _disputes.Verify(r => r.AddAsync(It.IsAny<Dispute>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Le_vendeur_aussi_peut_ouvrir_un_litige()
    {
        GivenTransaction(PaidTransaction());
        GivenExistingDisputeOnTransaction(null);

        var act = () => OpenHandler().Handle(
            new OpenDisputeCommand(1, VendorId, "buyer_unreachable", "Acheteur injoignable.", []), default);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Une_transaction_ne_porte_qu_un_seul_litige()
    {
        GivenTransaction(PaidTransaction());
        GivenExistingDisputeOnTransaction(OpenDispute());

        var act = () => OpenHandler().Handle(
            new OpenDisputeCommand(1, BuyerId, "not_received", "Deuxieme reclamation.", []), default);

        await act.Should().ThrowAsync<BusinessRuleException>();
    }

    [Fact]
    public async Task Un_litige_sur_une_transaction_inconnue_est_refuse()
    {
        _transactions.Setup(r => r.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Transaction?)null);

        var act = () => OpenHandler().Handle(
            new OpenDisputeCommand(404, BuyerId, "not_received", "Colis jamais recu.", []), default);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Une_transaction_cloturee_n_accepte_plus_de_litige()
    {
        // Etat terminal : la machine a etats refuse la transition et l'erreur
        // doit remonter comme une regle metier, pas comme une panne.
        var transaction = PaidTransaction();
        transaction.Transition(TransactionStatus.InShipping);
        transaction.Transition(TransactionStatus.Delivered);
        transaction.Transition(TransactionStatus.Closed);
        GivenTransaction(transaction);
        GivenExistingDisputeOnTransaction(null);

        var act = () => OpenHandler().Handle(
            new OpenDisputeCommand(1, BuyerId, "not_received", "Trop tard.", []), default);

        await act.Should().ThrowAsync<DomainException>();
    }

    // --------------------------------------------------------------- resolution

    [Fact]
    public async Task Trancher_en_faveur_de_l_acheteur_rembourse_avant_d_ecrire_le_statut()
    {
        var transaction = PaidTransaction();
        transaction.Transition(TransactionStatus.Dispute);
        var dispute = OpenDispute();
        GivenDispute(dispute);
        GivenTransaction(transaction);

        await ResolveHandler().Handle(new ResolveDisputeCommand(1, "refunded", "Acheteur credible."), default);

        _payments.Verify(p => p.RefundAsync("pi_test", It.IsAny<CancellationToken>()), Times.Once);
        transaction.Status.Should().Be(TransactionStatus.Refunded);
        dispute.Status.Should().Be(DisputeStatus.Resolved);
    }

    [Fact]
    public async Task Trancher_en_faveur_du_vendeur_ne_rembourse_pas()
    {
        var transaction = PaidTransaction();
        transaction.Transition(TransactionStatus.Dispute);
        GivenDispute(OpenDispute());
        GivenTransaction(transaction);

        await ResolveHandler().Handle(new ResolveDisputeCommand(1, "resolved", "Livraison prouvee."), default);

        _payments.Verify(p => p.RefundAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        transaction.Status.Should().Be(TransactionStatus.Resolved);
    }

    [Fact]
    public async Task Un_litige_deja_tranche_ne_peut_pas_l_etre_deux_fois()
    {
        // Sans ce garde-fou, une double resolution en "refunded" declencherait
        // deux remboursements pour un seul encaissement.
        var dispute = OpenDispute();
        dispute.Resolve("Deja tranche.");
        GivenDispute(dispute);
        GivenTransaction(PaidTransaction());

        var act = () => ResolveHandler().Handle(new ResolveDisputeCommand(1, "refunded", "Encore."), default);

        await act.Should().ThrowAsync<BusinessRuleException>();
        _payments.Verify(p => p.RefundAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Un_litige_clos_ne_peut_plus_etre_tranche()
    {
        var dispute = OpenDispute();
        dispute.Close();
        GivenDispute(dispute);
        GivenTransaction(PaidTransaction());

        var act = () => ResolveHandler().Handle(new ResolveDisputeCommand(1, "resolved", "Note."), default);

        await act.Should().ThrowAsync<BusinessRuleException>();
    }

    [Fact]
    public async Task Une_decision_inconnue_est_rejetee_en_validation()
    {
        GivenDispute(OpenDispute());
        GivenTransaction(PaidTransaction());

        var act = () => ResolveHandler().Handle(new ResolveDisputeCommand(1, "peut-etre", "Note."), default);

        var thrown = await act.Should().ThrowAsync<ValidationException>();
        thrown.Which.Errors.Should().ContainKey("decision");
    }

    [Fact]
    public async Task Rembourser_sans_encaissement_est_refuse()
    {
        // Une transaction sans intention de paiement n'a jamais mis d'argent en
        // sequestre : afficher un remboursement serait un mensonge comptable.
        var transaction = PaidTransaction(paymentIntentId: null);
        transaction.Transition(TransactionStatus.Dispute);
        GivenDispute(OpenDispute());
        GivenTransaction(transaction);

        var act = () => ResolveHandler().Handle(new ResolveDisputeCommand(1, "refunded", "Note."), default);

        await act.Should().ThrowAsync<BusinessRuleException>();
        _payments.Verify(p => p.RefundAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Un_remboursement_refuse_par_le_prestataire_laisse_le_litige_ouvert()
    {
        var transaction = PaidTransaction();
        transaction.Transition(TransactionStatus.Dispute);
        var dispute = OpenDispute();
        GivenDispute(dispute);
        GivenTransaction(transaction);
        _payments.Setup(p => p.RefundAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("card_declined"));

        var act = () => ResolveHandler().Handle(new ResolveDisputeCommand(1, "refunded", "Note."), default);

        await act.Should().ThrowAsync<BusinessRuleException>();
        dispute.Status.Should().Be(DisputeStatus.Open);
        transaction.Status.Should().Be(TransactionStatus.Dispute);
        _publisher.Verify(p => p.Publish(It.IsAny<TransactionStatusChangedEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Resoudre_un_litige_inconnu_est_refuse()
    {
        GivenDispute(null);

        var act = () => ResolveHandler().Handle(new ResolveDisputeCommand(404, "resolved", "Note."), default);

        await act.Should().ThrowAsync<NotFoundException>();
    }
}
