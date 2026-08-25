using MediatR;
using SafeDeal.Application.Common.Exceptions;
using SafeDeal.Domain.Interfaces.Repositories;

namespace SafeDeal.Application.Auth.Commands.SetTwoFactor;

public class SetTwoFactorCommandHandler : IRequestHandler<SetTwoFactorCommand>
{
    private readonly IUserRepository _users;
    public SetTwoFactorCommandHandler(IUserRepository users) => _users = users;

    public async Task Handle(SetTwoFactorCommand request, CancellationToken ct)
    {
        var user = await _users.GetByIdAsync(request.UserId, ct)
            ?? throw new NotFoundException("User", request.UserId);

        user.SetTwoFactor(request.Enabled);
        await _users.UpdateAsync(user, ct);
    }
}
