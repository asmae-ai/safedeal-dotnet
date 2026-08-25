using MediatR;
using SafeDeal.Application.Common.Exceptions;
using SafeDeal.Application.Common.Interfaces;
using SafeDeal.Application.Transactions.Commands.CreateTransaction;
using SafeDeal.Application.Transactions.DTOs;
using SafeDeal.Domain.Entities;
using SafeDeal.Domain.Enums;
using SafeDeal.Domain.Events;
using SafeDeal.Domain.Exceptions;
using SafeDeal.Domain.Interfaces.Repositories;

namespace SafeDeal.Application.Disputes.Commands.OpenDispute;

public class OpenDisputeCommandHandler : IRequestHandler<OpenDisputeCommand, TransactionDto>
{
    private readonly IDisputeRepository _disputes;
    private readonly ITransactionRepository _transactions;
    private readonly IUserRepository _users;
    private readonly IApplicationDbContext _context;
    private readonly IPublisher _publisher;

    public OpenDisputeCommandHandler(
        IDisputeRepository disputes,
        ITransactionRepository transactions,
        IUserRepository users,
        IApplicationDbContext context,
        IPublisher publisher)
    {
        _disputes = disputes;
        _transactions = transactions;
        _users = users;
        _context = context;
        _publisher = publisher;
    }

    public async Task<TransactionDto> Handle(OpenDisputeCommand request, CancellationToken ct)
    {
        var transaction = await _transactions.GetByIdAsync(request.TransactionId, ct)
            ?? throw new NotFoundException("Transaction", request.TransactionId);

        if (transaction.VendorId != request.BuyerId && transaction.BuyerId != request.BuyerId)
            throw new ForbiddenException("You are not a party to this transaction.");

        // Une transaction ne porte qu'un seul litige. Sans ce contrôle la contrainte 1-1
        // de la base remonterait en 500 au lieu d'une erreur métier lisible.
        var existing = await _disputes.GetByTransactionIdAsync(request.TransactionId, ct);
        if (existing is not null)
            throw new BusinessRuleException("A dispute has already been opened for this transaction.");

        var dispute = Dispute.Create(request.TransactionId, request.BuyerId, request.Category, request.Description);

        // La réclamation initiale est le premier échange du dossier : elle porte son
        // auteur et ses pièces jointes, comme les réponses qui suivront.
        dispute.AddMessage(request.BuyerId, request.Description, request.FilePaths);

        // Créer le litige et geler la transaction sont indissociables : un litige
        // enregistré sur une transaction restée libérable laisserait les fonds sortir.
        await _context.ExecuteInTransactionAsync(async () =>
        {
            await _disputes.AddAsync(dispute, ct);
            transaction.Transition(TransactionStatus.Dispute, $"Dispute #{dispute.Id} opened.");
            await _transactions.UpdateAsync(transaction, ct);
        }, ct);

        await _publisher.Publish(new DisputeOpenedEvent(transaction.Id, dispute.Id), ct);
        await _publisher.Publish(new TransactionStatusChangedEvent(transaction.Id, TransactionStatus.Dispute), ct);

        var vendor = await _users.GetByIdAsync(transaction.VendorId, ct);
        var buyer = transaction.BuyerId.HasValue ? await _users.GetByIdAsync(transaction.BuyerId.Value, ct) : null;
        return CreateTransactionCommandHandler.MapToDto(transaction, vendor!, buyer);
    }
}
