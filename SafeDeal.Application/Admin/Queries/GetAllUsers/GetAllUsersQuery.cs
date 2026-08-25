using MediatR;
using SafeDeal.Application.Admin.DTOs;
using SafeDeal.Application.Common.Models;

namespace SafeDeal.Application.Admin.Queries.GetAllUsers;

public record GetAllUsersQuery(
    int Page = 1,
    int PageSize = 20,
    string? Search = null,
    string? Role = null) : IRequest<PagedResult<AdminUserDto>>
{
    /// <summary>Page effectivement interrogee, ramenee dans les bornes valides.</summary>
    public int SafePage => Paging.NormalizePage(Page);

    /// <summary>Taille effective, plafonnee pour ne pas laisser un client exiger toute la table.</summary>
    public int SafePageSize => Paging.NormalizePageSize(PageSize);
}
