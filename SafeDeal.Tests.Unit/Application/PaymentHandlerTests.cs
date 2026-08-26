using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SafeDeal.Application.Common.Exceptions;
using SafeDeal.Application.Transactions.Commands.PayTransaction;
using SafeDeal.Domain.Entities;
using SafeDeal.Domain.Enums;
using SafeDeal.Domain.Events;
using SafeDeal.Domain.Interfaces.Repositories;

namespace SafeDeal.Tests.Unit.Application;

/// <summary>
/// L'encaissement est le point ou l'argent entre en sequestre : il est declenche
/// par un webhook que Stripe rejoue jusqu'a obtenir un 2xx, donc chaque cas
/// limite compte.
/// </summary>
public class PaymentHandlerTests
{
    private readonly Mock<ITransactionRepository> _transactions = new();
    private readonly Mock<IPublisher> _publisher = new();

    private PayTransactionCommandHandler Handler() => new(
        _transactions.Object,
        _publisher.Object,
        NullLogger<PayTransactionCommandHandler>.Instance);

    private static Transaction PendingPayment() =>
        Transaction.Create("Vélo de route", 1200m, "MAD", vendorId: 1);

    private static Transaction Paid()
    {
        var transaction = PendingPayment();
        transaction.Claim(buyerId: 2);
        transaction.SetStripePaymentIntent("pi_existing");
        transaction.Transition(TransactionStatus.PaymentReceived);
        return transaction;
    }

    [Fact]
    public async Task Un_paiement_place_les_fonds_en_sequestre()
    {
        var transaction = PendingPayment();
        _transactions.Setup(r => r.GetByIdAsync(7, It.IsAny<CancellationToken>())).ReturnsAsync(transaction);

        await Handler().Handle(new PayTransactionCommand(7, "cs_test_1", "pi_test_1"), default);

        transaction.Status.Should().Be(TransactionStatus.PaymentReceived);
        transaction.StripeSessionId.Should().Be("cs_test_1");
        transaction.StripePaymentIntentId.Should().Be("pi_test_1");
        _transactions.Verify(r => r.UpdateAsync(transaction, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Le_paiement_annonce_le_changement_de_statut()
    {
        _transactions.Setup(r => r.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PendingPayment());

        await Handler().Handle(new PayTransactionCommand(7, "cs", "pi"), default);

        _publisher.Verify(p => p.Publish(
            It.Is<TransactionStatusChangedEvent>(e => e.NewStatus == TransactionStatus.PaymentReceived),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Un_rejeu_de_webhook_sur_une_transaction_deja_payee_est_sans_effet()
    {
        // Stripe reemet jusqu'a acquittement : un second passage ne doit ni
        // ecraser l'intention de paiement d'origine, ni renotifier les parties.
        var transaction = Paid();
        _transactions.Setup(r => r.GetByIdAsync(7, It.IsAny<CancellationToken>())).ReturnsAsync(transaction);

        await Handler().Handle(new PayTransactionCommand(7, "cs_replay", "pi_replay"), default);

        transaction.StripePaymentIntentId.Should().Be("pi_existing");
        _transactions.Verify(r => r.UpdateAsync(It.IsAny<Transaction>(), It.IsAny<CancellationToken>()), Times.Never);
        _publisher.Verify(p => p.Publish(It.IsAny<TransactionStatusChangedEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(TransactionStatus.Cancelled)]
    [InlineData(TransactionStatus.Refunded)]
    public async Task Un_webhook_tardif_ne_ressuscite_pas_une_transaction_terminee(TransactionStatus status)
    {
        // Une transaction annulee puis payee tardivement doit rester annulee :
        // reencaisser ici remettrait en sequestre un argent deja rendu.
        var transaction = PendingPayment();
        if (status == TransactionStatus.Refunded)
        {
            transaction.Claim(2);
            transaction.Transition(TransactionStatus.PaymentReceived);
            transaction.Transition(TransactionStatus.InShipping);
            transaction.Transition(TransactionStatus.Refunded);
        }
        else
        {
            transaction.Transition(TransactionStatus.Cancelled);
        }

        _transactions.Setup(r => r.GetByIdAsync(7, It.IsAny<CancellationToken>())).ReturnsAsync(transaction);

        await Handler().Handle(new PayTransactionCommand(7, "cs", "pi"), default);

        transaction.Status.Should().Be(status);
        _transactions.Verify(r => r.UpdateAsync(It.IsAny<Transaction>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Un_paiement_visant_une_transaction_inconnue_est_refuse()
    {
        _transactions.Setup(r => r.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Transaction?)null);

        var act = () => Handler().Handle(new PayTransactionCommand(999, "cs", "pi"), default);

        await act.Should().ThrowAsync<NotFoundException>();
    }
}
