using MediatR;
using SafeDeal.Application.Common.Exceptions;
using SafeDeal.Domain.Interfaces.Repositories;

namespace SafeDeal.Application.Auth.Commands.ChangePassword;

public class ChangePasswordCommandHandler : IRequestHandler<ChangePasswordCommand>
{
    private readonly IUserRepository _users;
    public ChangePasswordCommandHandler(IUserRepository users) => _users = users;

    public async Task Handle(ChangePasswordCommand request, CancellationToken ct)
    {
        var user = await _users.GetByIdAsync(request.UserId, ct)
            ?? throw new NotFoundException("User", request.UserId);

        if (!BCrypt.Net.BCrypt.Verify(request.CurrentPassword, user.PasswordHash))
            throw new ValidationException(new Dictionary<string, string[]>
            {
                ["current_password"] = ["Current password is incorrect."]
            });

        user.UpdatePasswordHash(BCrypt.Net.BCrypt.HashPassword(request.Password));
        await _users.UpdateAsync(user, ct);
    }
}