using MediatR;
using SafeDeal.Application.Admin.DTOs;

namespace SafeDeal.Application.Admin.Queries.GetAllDisputes;

/// <param name="Status">"open" (défaut), "settled" ou "all".</param>
public record GetAllDisputesQuery(string Status = "open") : IRequest<IEnumerable<AdminDisputeDto>>;
