using MediatR;
using SafeDeal.Application.Common.Caching;
using SafeDeal.Application.Dashboard.DTOs;

namespace SafeDeal.Application.Dashboard.Queries.GetVendorDashboard;

public record GetVendorDashboardQuery(int VendorId) : IRequest<VendorDashboardDto>, ICachedQuery
{
    // Agregat de lecture : aucune decision metier ne depend de cette reponse.
    public string CacheScope => CacheScopes.User(VendorId);
    public string CacheKey => CacheKeys.VendorDashboard;
    public CacheProfile Profile => CacheProfile.Dashboard;
}
