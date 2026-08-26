using MediatR;
using SafeDeal.Application.Admin.DTOs;
using SafeDeal.Application.Common.Models;

namespace SafeDeal.Application.Admin.Queries.GetAllDisputes;

/// <param name="Status">"open" (défaut), "settled" ou "all".</param>
/// <param name="Page">Absente, la liste complète est rendue comme avant.</param>
public record GetAllDisputesQuery(string Status = "open", int? Page = null, int PageSize = 20)
    : IRequest<PagedResult<AdminDisputeDto>>, IOptionallyPagedQuery;
