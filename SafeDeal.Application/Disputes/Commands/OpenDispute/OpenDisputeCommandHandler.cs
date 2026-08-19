using MediatR;
using SafeDeal.Application.Common.Exceptions;
using SafeDeal.Application.Transactions.Commands.CreateTransaction;
using SafeDeal.Application.Transactions.DTOs;
using SafeDeal.Domain.Entities;
using SafeDeal.Domain.Events;
using SafeDeal.Domain.Interfaces.Repositories;

namespace SafeDeal.Application.Disputes.Commands.OpenDispute;

public class OpenDisputeCommandHandler : IRequestHandler<OpenDisputeCommand, TransactionDto>
{
    private readonly IDisputeRepository _disputes;
    private readonly ITransactionRepository _transactions;
    private readonly IUserRepository _users;
    private readonly IPublisher _publisher;

    public OpenDisputeCommandHandler(IDisputeRepository disputes, ITransactionRepository transactions, IUserRepository users, IPublisher publisher)
    {
        _disputes = disputes;
        _transactions = transactions;
        _users = users;
        _publisher = publisher;
    }

    public async Task<TransactionDto> Handle(OpenDisputeCommand request, CancellationToken ct)
    {
        var transaction = await _transactions.GetByIdAsync(request.TransactionId, ct)
            ?? throw new NotFoundException("Transaction", request.TransactionId);

        var dispute = Dispute.Create(request.TransactionId, request.BuyerId, request.Category, request.Description);
        await _disputes.AddAsync(dispute, ct);

        await _publisher.Publish(new DisputeOpenedEvent(transaction.Id, dispute.Id), ct);

        var vendor = await _users.GetByIdAsync(transaction.VendorId, ct);
        var buyer = transaction.BuyerId.HasValue ? await _users.GetByIdAsync(transaction.BuyerId.Value, ct) : null;
        return CreateTransactionCommandHandler.MapToDto(transaction, vendor!, buyer);
    }
}
