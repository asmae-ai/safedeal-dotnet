using MediatR;
using SafeDeal.Application.Admin.DTOs;
using SafeDeal.Application.Common.Caching;

namespace SafeDeal.Application.Admin.Queries.GetStatistics;

public record GetStatisticsQuery : IRequest<AdminStatsDto>, ICachedQuery
{
    public string CacheScope => CacheScopes.Admin;
    public string CacheKey => CacheKeys.AdminStats;
    public CacheProfile Profile => CacheProfile.AdminStats;
}
