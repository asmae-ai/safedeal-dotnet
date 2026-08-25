using MediatR;
using SafeDeal.Application.Dashboard.DTOs;

namespace SafeDeal.Application.Dashboard.Queries.GetAdminDashboard;

/// <param name="Range">"7d" (defaut), "30d" ou "12m" : granularite de la courbe de volume.</param>
public record GetAdminDashboardQuery(string Range = "7d") : IRequest<AdminDashboardDto>;
