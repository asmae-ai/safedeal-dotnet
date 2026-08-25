using MediatR;
using SafeDeal.Application.Common.Exceptions;
using SafeDeal.Application.Transactions.DTOs;
using SafeDeal.Domain.Entities;
using SafeDeal.Domain.Enums;
using SafeDeal.Domain.Interfaces.Repositories;
using SafeDeal.Application.Common.Extensions;
namespace SafeDeal.Application.Transactions.Commands.CreateTransaction;

public class CreateTransactionCommandHandler : IRequestHandler<CreateTransactionCommand, TransactionDto>
{
    private readonly ITransactionRepository _transactions;
    private readonly IUserRepository _users;

    public CreateTransactionCommandHandler(ITransactionRepository transactions, IUserRepository users)
    {
        _transactions = transactions;
        _users = users;
    }

    public async Task<TransactionDto> Handle(CreateTransactionCommand request, CancellationToken ct)
    {
        var vendor = await _users.GetByIdAsync(request.VendorId, ct)
            ?? throw new NotFoundException("User", request.VendorId);

        if (vendor.Role != UserRole.Vendor)
            throw new ForbiddenException("Only vendors can create transactions.");

        if (vendor.IdentityStatus != IdentityStatus.Approved)
            throw new ForbiddenException("Your identity must be verified before creating a transaction.");

        var transaction = Transaction.Create(request.Title, request.Amount, request.Currency, request.VendorId);
        await _transactions.AddAsync(transaction, ct);

        return MapToDto(transaction, vendor, null);
    }

    public static TransactionDto MapToDto(Transaction t, Domain.Entities.User vendor, Domain.Entities.User? buyer) => new(
        t.Id,
        t.SecureToken,
        t.Title,
        t.Amount.Amount.ToApiString(),
        t.Amount.Currency,
        t.Status.ToString().ToSnakeCase(),
        t.TrackingNumber,
        t.Carrier,
        new UserSummaryDto(vendor.Id, vendor.Name, vendor.Email),
        buyer is null ? null : new UserSummaryDto(buyer.Id, buyer.Name, buyer.Email),
        t.CreatedAt.ToString("o"),
        t.UpdatedAt.ToString("o"));
}