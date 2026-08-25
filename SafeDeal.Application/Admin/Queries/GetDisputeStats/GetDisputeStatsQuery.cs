using MediatR;
using SafeDeal.Application.Admin.DTOs;

namespace SafeDeal.Application.Admin.Queries.GetDisputeStats;

public record GetDisputeStatsQuery : IRequest<AdminQueueStatsDto>;
