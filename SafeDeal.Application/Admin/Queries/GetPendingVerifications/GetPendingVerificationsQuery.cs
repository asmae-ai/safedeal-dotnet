using MediatR;
using SafeDeal.Application.Admin.DTOs;
using SafeDeal.Application.Common.Models;

namespace SafeDeal.Application.Admin.Queries.GetPendingVerifications;

/// <param name="Page">Absente, la file complète est rendue comme avant.</param>
public record GetPendingVerificationsQuery(int? Page = null, int PageSize = 20)
    : IRequest<PagedResult<AdminVerificationDto>>, IOptionallyPagedQuery;
