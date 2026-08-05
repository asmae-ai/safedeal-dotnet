using MediatR;

namespace SafeDeal.Application.Admin.Commands.RejectIdentity;

public record RejectIdentityCommand(int UserId, string Reason) : IRequest;