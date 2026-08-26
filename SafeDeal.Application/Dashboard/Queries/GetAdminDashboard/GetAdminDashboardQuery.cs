using MediatR;
using SafeDeal.Application.Common.Caching;
using SafeDeal.Application.Dashboard.DTOs;

namespace SafeDeal.Application.Dashboard.Queries.GetAdminDashboard;

/// <param name="Range">"7d" (defaut), "30d" ou "12m" : granularite de la courbe de volume.</param>
public record GetAdminDashboardQuery(string Range = "7d") : IRequest<AdminDashboardDto>, ICachedQuery
{
    // La granularite fait partie de la cle : les trois courbes sont trois
    // reponses distinctes, qui ne doivent pas se remplacer l'une l'autre.
    public string CacheScope => CacheScopes.Admin;
    public string CacheKey => CacheKeys.AdminDashboard(Range);
    public CacheProfile Profile => CacheProfile.AdminStats;
}
