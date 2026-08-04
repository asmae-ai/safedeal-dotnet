using MediatR;
using SafeDeal.Application.Disputes.DTOs;

namespace SafeDeal.Application.Disputes.Queries.GetDispute;

public record GetDisputeQuery(int TransactionId, int UserId) : IRequest<DisputeDto>;