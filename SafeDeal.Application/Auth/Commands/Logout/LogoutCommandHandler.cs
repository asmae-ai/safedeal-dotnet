using MediatR;
using SafeDeal.Domain.Interfaces.Services;

namespace SafeDeal.Application.Auth.Commands.Logout;

public class LogoutCommandHandler : IRequestHandler<LogoutCommand>
{
    private readonly ITokenService _tokenService;
    public LogoutCommandHandler(ITokenService tokenService) => _tokenService = tokenService;

    public async Task Handle(LogoutCommand request, CancellationToken ct)
        => await _tokenService.BlacklistTokenAsync(request.Token, ct);
}