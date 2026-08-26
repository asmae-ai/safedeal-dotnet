using MediatR;
using SafeDeal.Application.Admin.DTOs;
using SafeDeal.Application.Common.Caching;

namespace SafeDeal.Application.Admin.Queries.GetDisputeStats;

public record GetDisputeStatsQuery : IRequest<AdminQueueStatsDto>, ICachedQuery
{
    public string CacheScope => CacheScopes.Admin;
    public string CacheKey => CacheKeys.DisputeStats;
    public CacheProfile Profile => CacheProfile.AdminStats;
}
