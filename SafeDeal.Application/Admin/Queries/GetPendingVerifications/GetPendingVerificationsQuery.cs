using MediatR;
using SafeDeal.Application.Admin.DTOs;

namespace SafeDeal.Application.Admin.Queries.GetPendingVerifications;

public record GetPendingVerificationsQuery : IRequest<IEnumerable<AdminVerificationDto>>;