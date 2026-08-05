using MediatR;
using SafeDeal.Application.Admin.DTOs;

namespace SafeDeal.Application.Admin.Queries.GetStatistics;

public record GetStatisticsQuery : IRequest<AdminStatsDto>;