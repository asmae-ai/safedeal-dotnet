using MediatR;
using SafeDeal.Application.Dashboard.DTOs;

namespace SafeDeal.Application.Dashboard.Queries.GetBuyerDashboard;

public record GetBuyerDashboardQuery(int BuyerId) : IRequest<BuyerDashboardDto>;
