using MediatR;

namespace SafeDeal.Application.Admin.Commands.ApproveIdentity;

public record ApproveIdentityCommand(int UserId) : IRequest;