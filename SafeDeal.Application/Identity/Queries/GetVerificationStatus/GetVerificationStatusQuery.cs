using MediatR;
using SafeDeal.Application.Identity.DTOs;

namespace SafeDeal.Application.Identity.Queries.GetVerificationStatus;

public record GetVerificationStatusQuery(int UserId) : IRequest<IdentityStatusDto>;