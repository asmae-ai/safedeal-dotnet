using MediatR;
using SafeDeal.Application.Dashboard.DTOs;

namespace SafeDeal.Application.Dashboard.Queries.GetVendorDashboard;

public record GetVendorDashboardQuery(int VendorId) : IRequest<VendorDashboardDto>;
