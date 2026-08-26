using MediatR;
using SafeDeal.Application.Admin.DTOs;
using SafeDeal.Application.Common.Caching;

namespace SafeDeal.Application.Admin.Queries.GetIdentityStats;

public record GetIdentityStatsQuery : IRequest<AdminQueueStatsDto>, ICachedQuery
{
    public string CacheScope => CacheScopes.Admin;
    public string CacheKey => CacheKeys.IdentityStats;
    public CacheProfile Profile => CacheProfile.AdminStats;
}
