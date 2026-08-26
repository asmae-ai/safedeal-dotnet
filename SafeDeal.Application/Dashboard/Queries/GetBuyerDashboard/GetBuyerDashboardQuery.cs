using MediatR;
using SafeDeal.Application.Common.Caching;
using SafeDeal.Application.Dashboard.DTOs;

namespace SafeDeal.Application.Dashboard.Queries.GetBuyerDashboard;

public record GetBuyerDashboardQuery(int BuyerId) : IRequest<BuyerDashboardDto>, ICachedQuery
{
    public string CacheScope => CacheScopes.User(BuyerId);
    public string CacheKey => CacheKeys.BuyerDashboard;
    public CacheProfile Profile => CacheProfile.Dashboard;
}
