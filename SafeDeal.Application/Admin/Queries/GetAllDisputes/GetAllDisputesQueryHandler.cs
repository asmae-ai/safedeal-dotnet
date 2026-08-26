using MediatR;
using SafeDeal.Application.Common.Extensions;
using Microsoft.EntityFrameworkCore;
using SafeDeal.Application.Admin.DTOs;
using SafeDeal.Application.Common.Interfaces;
using SafeDeal.Application.Common.Models;
using SafeDeal.Domain.Enums;

namespace SafeDeal.Application.Admin.Queries.GetAllDisputes;

public class GetAllDisputesQueryHandler : IRequestHandler<GetAllDisputesQuery, PagedResult<AdminDisputeDto>>
{
    private readonly IApplicationDbContext _context;
    public GetAllDisputesQueryHandler(IApplicationDbContext context) => _context = context;

    public async Task<PagedResult<AdminDisputeDto>> Handle(GetAllDisputesQuery request, CancellationToken ct)
    {
        var query = _context.Disputes
            .Include(d => d.OpenedBy)
            .Include(d => d.Transaction).ThenInclude(t => t.Vendor)
            .Include(d => d.Transaction).ThenInclude(t => t.Buyer)
            .AsQueryable();

        // Comparaisons explicites plutôt qu'un Contains sur collection : EF Core ne sait
        // pas traduire Contains sur un tableau d'enum et part en exception à l'exécution.
        query = request.Status switch
        {
            "settled" => query.Where(d => d.Status == DisputeStatus.Resolved || d.Status == DisputeStatus.Closed),
            "all" => query,
            _ => query.Where(d => d.Status != DisputeStatus.Resolved && d.Status != DisputeStatus.Closed)
        };

        // Le total porte sur le filtre, pas sur la page : c'est lui qui permet au
        // client de savoir combien de pages il reste.
        var total = await query.CountAsync(ct);

        // Le formatage (padding de la référence, décimales du montant, date ISO) ne se
        // traduit pas en SQL : on matérialise d'abord, on met en forme ensuite.
        var disputes = await query
            .OrderByDescending(d => d.CreatedAt)
            .Slice(request)
            .Select(d => new
            {
                d.Id,
                d.TransactionId,
                d.Category,
                d.Description,
                d.Status,
                d.CreatedAt,
                d.ResolutionNote,
                d.OpenedByUserId,
                Title = d.Transaction.Title,
                Amount = d.Transaction.Amount.Amount,
                Currency = d.Transaction.Amount.Currency,
                VendorId = d.Transaction.VendorId,
                VendorName = d.Transaction.Vendor.Name,
                VendorEmail = d.Transaction.Vendor.Email,
                BuyerName = d.Transaction.Buyer != null ? d.Transaction.Buyer.Name : null,
                BuyerEmail = d.Transaction.Buyer != null ? d.Transaction.Buyer.Email : null,
                MessagesCount = d.Messages.Count
            })
            .ToListAsync(ct);

        var dtos = disputes.Select(d => new AdminDisputeDto(
            d.Id,
            d.TransactionId,
            $"SD-{d.TransactionId:D6}",
            d.Title,
            d.Amount.ToApiString(),
            d.Currency,
            d.Category,
            d.Description,
            d.Status.ToString().ToLower(),
            d.CreatedAt.ToString("o"),
            d.OpenedByUserId == d.VendorId ? "vendor" : "buyer",
            d.BuyerName,
            d.BuyerEmail,
            d.VendorName,
            d.VendorEmail,
            d.MessagesCount,
            d.ResolutionNote)).ToList();

        return dtos.ToResult(request, total);
    }
}
