using MediatR;
using SafeDeal.Application.Admin.DTOs;

namespace SafeDeal.Application.Admin.Queries.GetIdentityStats;

public record GetIdentityStatsQuery : IRequest<AdminQueueStatsDto>;
