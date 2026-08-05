using MediatR;
using SafeDeal.Application.Disputes.DTOs;

namespace SafeDeal.Application.Admin.Queries.GetAllDisputes;

public record GetAllDisputesQuery : IRequest<IEnumerable<DisputeDto>>;