using MediatR;
using SafeDeal.Application.Common.Caching;
using SafeDeal.Application.Common.Exceptions;
using SafeDeal.Application.Transactions.DTOs;
using SafeDeal.Domain.Interfaces.Repositories;
using SafeDeal.Application.Transactions.Commands.CreateTransaction;
namespace SafeDeal.Application.Transactions.Commands.ClaimTransaction;

public class ClaimTransactionCommandHandler : IRequestHandler<ClaimTransactionCommand, TransactionDto>
{
    private readonly ITransactionRepository _transactions;
    private readonly IUserRepository _users;
    private readonly ICacheService _cache;

    public ClaimTransactionCommandHandler(
        ITransactionRepository transactions,
        IUserRepository users,
        ICacheService cache)
    {
        _transactions = transactions;
        _users = users;
        _cache = cache;
    }

    public async Task<TransactionDto> Handle(ClaimTransactionCommand request, CancellationToken ct)
    {
        var transaction = await _transactions.GetByTokenAsync(request.Token, ct)
            ?? throw new NotFoundException("Transaction", request.Token);

        var buyer = await _users.GetByIdAsync(request.BuyerId, ct)
            ?? throw new NotFoundException("User", request.BuyerId);

        transaction.Claim(request.BuyerId);
        await _transactions.UpdateAsync(transaction, ct);

        // La transaction entre au tableau de bord de l'acheteur et gagne son nom
        // sur celui du vendeur : les deux vues changent, aucun statut ne bouge.
        await _cache.InvalidateAsync(CacheScopes.User(request.BuyerId), ct);
        await _cache.InvalidateAsync(CacheScopes.User(transaction.VendorId), ct);
        await _cache.InvalidateAsync(CacheScopes.Admin, ct);

        var vendor = await _users.GetByIdAsync(transaction.VendorId, ct)!;
        return CreateTransactionCommandHandler.MapToDto(transaction, vendor!, buyer);
    }
}