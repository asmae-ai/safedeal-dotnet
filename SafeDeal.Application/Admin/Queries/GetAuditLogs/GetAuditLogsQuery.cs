using MediatR;
using SafeDeal.Application.Admin.DTOs;
using SafeDeal.Application.Common.Models;

namespace SafeDeal.Application.Admin.Queries.GetAuditLogs;

/// <summary>
/// Lecture du journal d'audit. Toujours paginée : la table ne fait que croître,
/// et une consultation ne doit jamais pouvoir la charger en entier.
/// </summary>
/// <param name="Action">Filtre exact sur le nom d'action, insensible à la casse.</param>
/// <param name="UserId">Restreint aux actions d'un compte.</param>
/// <param name="EntityType">Type d'entité visée ("Transaction", "Dispute"…).</param>
/// <param name="EntityId">Entité précise, combiné à <paramref name="EntityType"/>.</param>
/// <param name="SucceededOnly">Vrai pour les succès, faux pour les échecs, nul pour les deux.</param>
public record GetAuditLogsQuery(
    int Page = 1,
    int PageSize = 20,
    string? Action = null,
    int? UserId = null,
    string? EntityType = null,
    int? EntityId = null,
    bool? SucceededOnly = null) : IRequest<PagedResult<AuditLogDto>>
{
    public int SafePage => Paging.NormalizePage(Page);
    public int SafePageSize => Paging.NormalizePageSize(PageSize);
}
