using MediatR;
using SafeDeal.Application.Common.Models;
using SafeDeal.Application.Transactions.DTOs;

namespace SafeDeal.Application.Transactions.Queries.GetTransactions;

public record GetTransactionsQuery(int UserId, int Page = 1, int PageSize = 15) : IRequest<PagedResult<TransactionDto>>
{
    /// <summary>Page effectivement interrogee, ramenee dans les bornes valides.</summary>
    public int SafePage => Paging.NormalizePage(Page);

    /// <summary>Taille effective, plafonnee pour ne pas laisser un client exiger toute la table.</summary>
    public int SafePageSize => Paging.NormalizePageSize(PageSize);
}
